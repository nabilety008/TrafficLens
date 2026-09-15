using System.Net;
using TrafficLens.Core.Models;

namespace TrafficLens.Network.Tests;

public sealed class ConnectionKeyTests
{
    [Fact]
    public void SameFiveTuple_SamePid_KeysAreEqual()
    {
        var left = ConnectionKey.From(Con("10.0.0.1", 100, "8.8.8.8", 53, pid: 1));
        var right = ConnectionKey.From(Con("10.0.0.1", 100, "8.8.8.8", 53, pid: 1));

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void DifferentPid_SameEndpoint_KeysDiffer()
    {
        var left = ConnectionKey.From(Con("10.0.0.1", 100, "8.8.8.8", 53, pid: 1));
        var right = ConnectionKey.From(Con("10.0.0.1", 100, "8.8.8.8", 53, pid: 2));

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void DifferentPort_KeysDiffer()
    {
        var left = ConnectionKey.From(Con("10.0.0.1", 100, "8.8.8.8", 53, pid: 1));
        var right = ConnectionKey.From(Con("10.0.0.1", 101, "8.8.8.8", 53, pid: 1));

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void UdpRow_WithoutRemote_IsKeyedByLocalOnly()
    {
        var key = ConnectionKey.From(
            new ConnectionInfo(
                1, "go", ConnectionProtocol.Udp, ConnectionAddressFamily.Ipv4,
                IPAddress.Any, 5353, null, null, ConnectionState.Unknown));

        Assert.Equal(ConnectionProtocol.Udp, key.Protocol);
        Assert.Null(key.RemoteAddress);
        Assert.Null(key.RemotePort);
    }

    [Fact]
    public void TcpVsUdp_SameLocalPort_KeysDiffer()
    {
        var tcp = ConnectionKey.From(
            new ConnectionInfo(1, "a", ConnectionProtocol.Tcp, ConnectionAddressFamily.Ipv4,
                IPAddress.Any, 80, IPAddress.Parse("1.1.1.1"), 80, ConnectionState.Established));
        var udp = ConnectionKey.From(
            new ConnectionInfo(1, "a", ConnectionProtocol.Udp, ConnectionAddressFamily.Ipv4,
                IPAddress.Any, 80, null, null, ConnectionState.Unknown));

        Assert.NotEqual(tcp, udp);
    }

    private static ConnectionInfo Con(string local, int localPort, string remote, int remotePort, int pid) =>
        new(
            pid,
            "app",
            ConnectionProtocol.Tcp,
            ConnectionAddressFamily.Ipv4,
            IPAddress.Parse(local),
            localPort,
            IPAddress.Parse(remote),
            remotePort,
            ConnectionState.Established);
}