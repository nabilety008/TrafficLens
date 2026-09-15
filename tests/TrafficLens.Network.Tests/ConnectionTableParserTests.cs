using System.Net;
using TrafficLens.Core.Models;
using TrafficLens.Network.Connections;

namespace TrafficLens.Network.Tests;

public sealed class ConnectionTableParserTests
{
    [Fact]
    public void ParseTcpV4_TypicalRow_MapsAllFieldsAndByteOrder()
    {
        var buffer = BuildTable(
            TcpV4Row(state: 5, "192.168.1.20", 443, "93.184.216.34", 80, pid: 5000));

        var rows = ConnectionTableParser.ParseTcpV4(buffer);

        var row = Assert.Single(rows);
        Assert.Equal(ConnectionProtocol.Tcp, row.Protocol);
        Assert.Equal(ConnectionAddressFamily.Ipv4, row.AddressFamily);
        Assert.Equal(IPAddress.Parse("192.168.1.20"), row.LocalAddress);
        Assert.Equal(443, row.LocalPort);
        Assert.Equal(IPAddress.Parse("93.184.216.34"), row.RemoteAddress);
        Assert.Equal(80, row.RemotePort);
        Assert.Equal(ConnectionState.Established, row.State);
        Assert.Equal(5000, row.ProcessId);
    }

    [Fact]
    public void ParseTcpV4_MultipleRows_PreservesOrder()
    {
        var buffer = BuildTable(
            TcpV4Row(5, "10.0.0.1", 1000, "8.8.8.8", 53, 1),
            TcpV4Row(2, "10.0.0.2", 2000, "8.8.4.4", 53, 2));

        var rows = ConnectionTableParser.ParseTcpV4(buffer);

        Assert.Equal(2, rows.Count);
        Assert.Equal(IPAddress.Parse("10.0.0.1"), rows[0].LocalAddress);
        Assert.Equal(IPAddress.Parse("10.0.0.2"), rows[1].LocalAddress);
    }

    [Fact]
    public void ParseTcpV6_MapToBacketedEndpointFields()
    {
        var localBytes = IPAddress.Parse("::1").GetAddressBytes();
        var remoteBytes = IPAddress.Parse("2001:db8::1").GetAddressBytes();
        var buffer = BuildTable(
            TcpV6Row(state: 2, localBytes, scope: 0, localPort: 5433, remoteBytes, remoteScope: 0, remotePort: 443, pid: 700));

        var row = Assert.Single(ConnectionTableParser.ParseTcpV6(buffer));

        Assert.Equal(ConnectionAddressFamily.Ipv6, row.AddressFamily);
        Assert.Equal(IPAddress.IPv6Loopback, row.LocalAddress);
        Assert.Equal(5433, row.LocalPort);
        Assert.Equal(IPAddress.Parse("2001:db8::1"), row.RemoteAddress);
        Assert.Equal(443, row.RemotePort);
        Assert.Equal(ConnectionState.Listen, row.State);
        Assert.Equal(700, row.ProcessId);
    }

    [Fact]
    public void ParseUdpV4_RowHasNoFabricatedRemote()
    {
        var buffer = BuildTable(
            UdpV4Row("0.0.0.0", 5353, pid: 100));

        var row = Assert.Single(ConnectionTableParser.ParseUdpV4(buffer));

        Assert.Equal(ConnectionProtocol.Udp, row.Protocol);
        Assert.Equal(ConnectionAddressFamily.Ipv4, row.AddressFamily);
        Assert.Equal(IPAddress.Any, row.LocalAddress);
        Assert.Equal(5353, row.LocalPort);
        Assert.Null(row.RemoteAddress);
        Assert.Null(row.RemotePort);
        Assert.Equal(ConnectionState.Unknown, row.State);
        Assert.Equal(100, row.ProcessId);
    }

    [Fact]
    public void ParseUdpV6_RowHasNoFabricatedRemote()
    {
        var localBytes = IPAddress.Parse("fe80::1").GetAddressBytes();
        var buffer = BuildTable(
            UdpV6Row(localBytes, scope: 2, localPort: 123, pid: 200));

        var row = Assert.Single(ConnectionTableParser.ParseUdpV6(buffer));

        Assert.Equal(ConnectionAddressFamily.Ipv6, row.AddressFamily);
        Assert.Equal(IPAddress.Parse("fe80::1"), row.LocalAddress);
        Assert.Equal(123, row.LocalPort);
        Assert.Null(row.RemoteAddress);
        Assert.Null(row.RemotePort);
        Assert.Equal(200, row.ProcessId);
    }

    [Fact]
    public void Parse_EmptyBuffer_ReturnsEmptyList()
    {
        Assert.Empty(ConnectionTableParser.ParseTcpV4([]));
        Assert.Empty(ConnectionTableParser.ParseTcpV6([]));
        Assert.Empty(ConnectionTableParser.ParseUdpV4([]));
        Assert.Empty(ConnectionTableParser.ParseUdpV6([]));
    }

    [Fact]
    public void Parse_TruncatedBuffer_ClampsToPresentRows()
    {
        // Header claims 5 entries but only 2 rows are present.
        var header = BitConverter.GetBytes(5);
        var row = TcpV4Row(5, "10.0.0.1", 100, "8.8.8.8", 53, 1);
        var row2 = TcpV4Row(5, "10.0.0.2", 200, "8.8.8.8", 53, 2);
        var buffer = new byte[4 + row.Length + row2.Length];
        Array.Copy(header, buffer, 4);
        Array.Copy(row, 0, buffer, 4, row.Length);
        Array.Copy(row2, 0, buffer, 4 + row.Length, row2.Length);

        var rows = ConnectionTableParser.ParseTcpV4(buffer);

        Assert.Equal(2, rows.Count);
    }

    [Theory]
    [InlineData(1, ConnectionState.Closed)]
    [InlineData(2, ConnectionState.Listen)]
    [InlineData(3, ConnectionState.SynSent)]
    [InlineData(4, ConnectionState.SynReceived)]
    [InlineData(5, ConnectionState.Established)]
    [InlineData(6, ConnectionState.FinWait1)]
    [InlineData(7, ConnectionState.FinWait2)]
    [InlineData(8, ConnectionState.CloseWait)]
    [InlineData(9, ConnectionState.Closing)]
    [InlineData(10, ConnectionState.LastAck)]
    [InlineData(11, ConnectionState.TimeWait)]
    public void ParseTcpV4_AllStateCodes_MapToEnums(uint stateCode, ConnectionState expected)
    {
        var buffer = BuildTable(
            TcpV4Row(stateCode, "10.0.0.1", 100, "8.8.8.8", 53, 1));

        var row = Assert.Single(ConnectionTableParser.ParseTcpV4(buffer));

        Assert.Equal(expected, row.State);
    }

    [Fact]
    public void ParseTcpV4_UnknownStateCode_MapsToUnknown()
    {
        var buffer = BuildTable(
            TcpV4Row(99, "10.0.0.1", 100, "8.8.8.8", 53, 1));

        var row = Assert.Single(ConnectionTableParser.ParseTcpV4(buffer));

        Assert.Equal(ConnectionState.Unknown, row.State);
    }

    private static byte[] BuildTable(params byte[][] rows)
    {
        var total = rows.Sum(r => r.Length);
        var buffer = new byte[4 + total];
        Array.Copy(BitConverter.GetBytes(rows.Length), buffer, 4);
        var offset = 4;
        foreach (var row in rows)
        {
            Array.Copy(row, 0, buffer, offset, row.Length);
            offset += row.Length;
        }

        return buffer;
    }

    private static byte[] TcpV4Row(
        uint state,
        string local,
        int localPort,
        string remote,
        int remotePort,
        int pid)
    {
        var row = new byte[24];
        Array.Copy(BitConverter.GetBytes(state), 0, row, 0, 4);
        Array.Copy(IPAddress.Parse(local).GetAddressBytes(), 0, row, 4, 4);
        Array.Copy(PortDword(localPort), 0, row, 8, 4);
        Array.Copy(IPAddress.Parse(remote).GetAddressBytes(), 0, row, 12, 4);
        Array.Copy(PortDword(remotePort), 0, row, 16, 4);
        Array.Copy(BitConverter.GetBytes(pid), 0, row, 20, 4);
        return row;
    }

    private static byte[] TcpV6Row(
        uint state,
        byte[] localBytes,
        uint scope,
        int localPort,
        byte[] remoteBytes,
        uint remoteScope,
        int remotePort,
        int pid)
    {
        var row = new byte[56];
        Array.Copy(localBytes, 0, row, 0, 16);
        Array.Copy(BitConverter.GetBytes(scope), 0, row, 16, 4);
        Array.Copy(PortDword(localPort), 0, row, 20, 4);
        Array.Copy(remoteBytes, 0, row, 24, 16);
        Array.Copy(BitConverter.GetBytes(remoteScope), 0, row, 40, 4);
        Array.Copy(PortDword(remotePort), 0, row, 44, 4);
        Array.Copy(BitConverter.GetBytes(state), 0, row, 48, 4);
        Array.Copy(BitConverter.GetBytes(pid), 0, row, 52, 4);
        return row;
    }

    private static byte[] UdpV4Row(string local, int localPort, int pid)
    {
        var row = new byte[12];
        Array.Copy(IPAddress.Parse(local).GetAddressBytes(), 0, row, 0, 4);
        Array.Copy(PortDword(localPort), 0, row, 4, 4);
        Array.Copy(BitConverter.GetBytes(pid), 0, row, 8, 4);
        return row;
    }

    private static byte[] UdpV6Row(byte[] localBytes, uint scope, int localPort, int pid)
    {
        var row = new byte[28];
        Array.Copy(localBytes, 0, row, 0, 16);
        Array.Copy(BitConverter.GetBytes(scope), 0, row, 16, 4);
        Array.Copy(PortDword(localPort), 0, row, 20, 4);
        Array.Copy(BitConverter.GetBytes(pid), 0, row, 24, 4);
        return row;
    }

    /// <summary>
    /// The native table stores the 16-bit port in network byte order inside a
    /// DWORD; in memory the low two bytes are then [portHi, portLo].
    /// </summary>
    private static byte[] PortDword(int port) =>
        new[] { (byte)((port >> 8) & 0xFF), (byte)(port & 0xFF), (byte)0, (byte)0 };
}