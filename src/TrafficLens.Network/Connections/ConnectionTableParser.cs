using System.Buffers.Binary;
using System.Net;
using TrafficLens.Core.Models;

namespace TrafficLens.Network.Connections;

/// <summary>
/// Pure, allocation-light parsing of the Windows IP Helper extended table
/// buffers into <see cref="ConnectionInfo"/> rows. No P/Invoke here: tests feed
/// synthetic byte buffers matching the MIB_*_OWNER_PID layouts. Ports in these
/// tables are stored in network byte order inside a DWORD; IPv4 addresses and
/// IPv6 addresses are stored as raw bytes in wire order. Rows whose remote
/// endpoint is not meaningful (UDP) are returned with null remote fields — a
/// remote endpoint is never synthesized.
/// </summary>
public static class ConnectionTableParser
{
    private const int EntryCountSize = 4;
    private const int TcpV4RowSize = 24;
    private const int TcpV6RowSize = 56;
    private const int UdpV4RowSize = 12;
    private const int UdpV6RowSize = 28;

    public static IReadOnlyList<ConnectionInfo> ParseTcpV4(ReadOnlySpan<byte> buffer)
    {
        var count = EntryCount(buffer, TcpV4RowSize);
        var list = new List<ConnectionInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var offset = EntryCountSize + i * TcpV4RowSize;
            var state = ReadState(buffer, offset);
            var local = ReadIpV4(buffer, offset + 4);
            var localPort = ReadPort(buffer, offset + 8);
            var remote = ReadIpV4(buffer, offset + 12);
            var remotePort = ReadPort(buffer, offset + 16);
            var pid = (int)ReadUInt32(buffer, offset + 20);
            list.Add(CreateRow(pid, ConnectionProtocol.Tcp, ConnectionAddressFamily.Ipv4,
                local, localPort, remote, remotePort, state));
        }

        return list;
    }

    public static IReadOnlyList<ConnectionInfo> ParseTcpV6(ReadOnlySpan<byte> buffer)
    {
        var count = EntryCount(buffer, TcpV6RowSize);
        var list = new List<ConnectionInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var offset = EntryCountSize + i * TcpV6RowSize;
            var local = ReadIpV6(buffer, offset);
            var localPort = ReadPort(buffer, offset + 20);
            var remote = ReadIpV6(buffer, offset + 24);
            var remotePort = ReadPort(buffer, offset + 44);
            var state = ReadState(buffer, offset + 48);
            var pid = (int)ReadUInt32(buffer, offset + 52);
            list.Add(CreateRow(pid, ConnectionProtocol.Tcp, ConnectionAddressFamily.Ipv6,
                local, localPort, remote, remotePort, state));
        }

        return list;
    }

    public static IReadOnlyList<ConnectionInfo> ParseUdpV4(ReadOnlySpan<byte> buffer)
    {
        var count = EntryCount(buffer, UdpV4RowSize);
        var list = new List<ConnectionInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var offset = EntryCountSize + i * UdpV4RowSize;
            var local = ReadIpV4(buffer, offset);
            var localPort = ReadPort(buffer, offset + 4);
            var pid = (int)ReadUInt32(buffer, offset + 8);
            list.Add(CreateRow(pid, ConnectionProtocol.Udp, ConnectionAddressFamily.Ipv4,
                local, localPort, null, null, ConnectionState.Unknown));
        }

        return list;
    }

    public static IReadOnlyList<ConnectionInfo> ParseUdpV6(ReadOnlySpan<byte> buffer)
    {
        var count = EntryCount(buffer, UdpV6RowSize);
        var list = new List<ConnectionInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var offset = EntryCountSize + i * UdpV6RowSize;
            var local = ReadIpV6(buffer, offset);
            var localPort = ReadPort(buffer, offset + 20);
            var pid = (int)ReadUInt32(buffer, offset + 24);
            list.Add(CreateRow(pid, ConnectionProtocol.Udp, ConnectionAddressFamily.Ipv6,
                local, localPort, null, null, ConnectionState.Unknown));
        }

        return list;
    }

    private static int EntryCount(ReadOnlySpan<byte> buffer, int rowSize)
    {
        if (buffer.Length < EntryCountSize)
        {
            return 0;
        }

        var declared = (int)ReadUInt32(buffer, 0);
        var maxFit = (buffer.Length - EntryCountSize) / rowSize;
        return Math.Max(0, Math.Min(declared, maxFit));
    }

    private static ConnectionInfo CreateRow(
        int pid,
        ConnectionProtocol protocol,
        ConnectionAddressFamily family,
        IPAddress local,
        int localPort,
        IPAddress? remote,
        int? remotePort,
        ConnectionState state) =>
        new(
            pid,
            null,
            protocol,
            family,
            local,
            localPort,
            remote,
            remotePort,
            state,
            Timestamp: DateTime.UtcNow);

    private static uint ReadUInt32(ReadOnlySpan<byte> buffer, int offset) =>
        offset + 4 <= buffer.Length ? BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset, 4)) : 0;

    private static int ReadPort(ReadOnlySpan<byte> buffer, int offset)
    {
        var raw = ReadUInt32(buffer, offset);
        return ((int)((raw & 0xFF) << 8) | (int)((raw >> 8) & 0xFF)) & 0xFFFF;
    }

    private static IPAddress ReadIpV4(ReadOnlySpan<byte> buffer, int offset)
    {
        Span<byte> bytes = stackalloc byte[4];
        if (offset + 4 <= buffer.Length)
        {
            buffer.Slice(offset, 4).CopyTo(bytes);
        }

        return new IPAddress(bytes);
    }

    private static IPAddress ReadIpV6(ReadOnlySpan<byte> buffer, int offset)
    {
        Span<byte> bytes = stackalloc byte[16];
        if (offset + 16 <= buffer.Length)
        {
            buffer.Slice(offset, 16).CopyTo(bytes);
        }

        return new IPAddress(bytes);
    }

    private static ConnectionState ReadState(ReadOnlySpan<byte> buffer, int offset) =>
        ReadUInt32(buffer, offset) switch
        {
            1 => ConnectionState.Closed,
            2 => ConnectionState.Listen,
            3 => ConnectionState.SynSent,
            4 => ConnectionState.SynReceived,
            5 => ConnectionState.Established,
            6 => ConnectionState.FinWait1,
            7 => ConnectionState.FinWait2,
            8 => ConnectionState.CloseWait,
            9 => ConnectionState.Closing,
            10 => ConnectionState.LastAck,
            11 => ConnectionState.TimeWait,
            _ => ConnectionState.Unknown
        };
}