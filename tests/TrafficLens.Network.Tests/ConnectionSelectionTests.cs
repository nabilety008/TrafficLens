using System.Net;
using TrafficLens.Core.Models;
using TrafficLens.Core.Selection;

namespace TrafficLens.Network.Tests;

public sealed class ConnectionSelectionTests
{
    private static ConnectionInfo Tcp(
        string local,
        int localPort,
        string remote,
        int remotePort,
        ConnectionState state,
        int pid,
        string? name,
        ConnectionAddressFamily family = ConnectionAddressFamily.Ipv4) =>
        new(pid, name, ConnectionProtocol.Tcp, family, IPAddress.Parse(local), localPort,
            IPAddress.Parse(remote), remotePort, state);

    private static ConnectionInfo Udp(
        string local,
        int localPort,
        string? remote,
        int? remotePort,
        int pid,
        string? name) =>
        new(pid, name, ConnectionProtocol.Udp, ConnectionAddressFamily.Ipv4, IPAddress.Parse(local), localPort,
            remote is null ? null : IPAddress.Parse(remote), remotePort, ConnectionState.Unknown);

    [Fact]
    public void Filter_All_MatchesEverything()
    {
        var established = Tcp("1", 1, "2", 2, ConnectionState.Established, 1, "a");
        var listening = Tcp("1", 1, "0.0.0.0", 0, ConnectionState.Listen, 1, "a");
        var udp = Udp("1", 1, null, null, 1, "a");

        Assert.True(ConnectionFiltering.Matches(established, ConnectionFilter.All));
        Assert.True(ConnectionFiltering.Matches(listening, ConnectionFilter.All));
        Assert.True(ConnectionFiltering.Matches(udp, ConnectionFilter.All));
    }

    [Fact]
    public void Filter_Established_MatchesActiveTcpAndConnectedUdpOnly()
    {
        var established = Tcp("1", 1, "2", 2, ConnectionState.Established, 1, "a");
        var listening = Tcp("1", 1, "0.0.0.0", 0, ConnectionState.Listen, 1, "a");
        var udpUnconnected = Udp("1", 1, null, null, 1, "a");
        var udpQueried = Udp("1", 1, "2", 2, 1, "a");

        Assert.True(ConnectionFiltering.Matches(established, ConnectionFilter.Established));
        Assert.False(ConnectionFiltering.Matches(listening, ConnectionFilter.Established));
        Assert.False(ConnectionFiltering.Matches(udpUnconnected, ConnectionFilter.Established));
        Assert.True(ConnectionFiltering.Matches(udpQueried, ConnectionFilter.Established));
    }

    [Fact]
    public void Filter_Listening_MatchesListenTcpOnly()
    {
        var listening = Tcp("1", 1, "0.0.0.0", 0, ConnectionState.Listen, 1, "a");
        var established = Tcp("1", 1, "2", 2, ConnectionState.Established, 1, "a");

        Assert.True(ConnectionFiltering.Matches(listening, ConnectionFilter.Listening));
        Assert.False(ConnectionFiltering.Matches(established, ConnectionFilter.Listening));
    }

    [Fact]
    public void Filter_Protocol_MatchesByProtocol()
    {
        var tcp = Tcp("1", 1, "2", 2, ConnectionState.Established, 1, "a");
        var udp = Udp("1", 1, null, null, 1, "a");

        Assert.True(ConnectionFiltering.Matches(tcp, ConnectionFilter.Tcp));
        Assert.False(ConnectionFiltering.Matches(udp, ConnectionFilter.Tcp));
        Assert.True(ConnectionFiltering.Matches(udp, ConnectionFilter.Udp));
        Assert.False(ConnectionFiltering.Matches(tcp, ConnectionFilter.Udp));
    }

    [Fact]
    public void Filter_Family_MatchesByAddressFamily()
    {
        var v4 = Tcp("1", 1, "2", 2, ConnectionState.Established, 1, "a");
        var v6 = Tcp("::1", 1, "::2", 2, ConnectionState.Established, 1, "a", ConnectionAddressFamily.Ipv6);

        Assert.True(ConnectionFiltering.Matches(v4, ConnectionFilter.Ipv4));
        Assert.False(ConnectionFiltering.Matches(v4, ConnectionFilter.Ipv6));
        Assert.True(ConnectionFiltering.Matches(v6, ConnectionFilter.Ipv6));
        Assert.False(ConnectionFiltering.Matches(v6, ConnectionFilter.Ipv4));
    }

    [Fact]
    public void Search_MatchesByNamePidLocalRemoteAndEmptyQuery()
    {
        var connection = Tcp("192.168.1.20", 443, "93.184.216.34", 80, ConnectionState.Established, 5000, "chrome");

        Assert.True(ConnectionFiltering.MatchesSearch(connection, "", "chrome"));
        Assert.True(ConnectionFiltering.MatchesSearch(connection, "chrome", "chrome"));
        Assert.True(ConnectionFiltering.MatchesSearch(connection, "5000", "chrome"));
        Assert.True(ConnectionFiltering.MatchesSearch(connection, "192.168.1.20", "chrome"));
        Assert.True(ConnectionFiltering.MatchesSearch(connection, "443", "chrome"));
        Assert.True(ConnectionFiltering.MatchesSearch(connection, "93.184.216.34", "chrome"));
        Assert.True(ConnectionFiltering.MatchesSearch(connection, "80", "chrome"));
        Assert.False(ConnectionFiltering.MatchesSearch(connection, "not-there", "chrome"));
    }

    [Fact]
    public void Search_MatchesLocalizedUnknownDisplayName()
    {
        var connection = Tcp("10.0.0.5", 1, "1.1.1.1", 2, ConnectionState.Established, 9, null);

        Assert.True(ConnectionFiltering.MatchesSearch(connection, "unknown", "Unknown Process"));
        Assert.False(ConnectionFiltering.MatchesSearch(connection, "unknown", "other"));
    }

    [Fact]
    public void Sort_Default_PutsEstablishedFirstThenByNameThenPid()
    {
        var connections = new List<ConnectionInfo>
        {
            Tcp("1", 1, "2", 2, ConnectionState.Listen, 3, "zeta"),
            Tcp("1", 2, "2", 2, ConnectionState.Established, 1, "Alpha"),
            Tcp("1", 3, "2", 2, ConnectionState.Established, 2, "alpha"),
            Udp("1", 4, null, null, 1, "udp-unconn"),
            Udp("1", 5, "2", 2, 1, "udp-conn")
        };

        connections.Sort(ConnectionSort.Create(ConnectionSortKey.Default));

        // Established TCP rows first (name tie-break), then connected UDP, then
        // listening TCP, then unconnected UDP.
        Assert.Equal("Alpha", connections[0].ProcessName);
        Assert.Equal("alpha", connections[1].ProcessName);
        Assert.Equal(ConnectionProtocol.Udp, connections[2].Protocol);
        Assert.NotNull(connections[2].RemoteAddress);
        Assert.Equal(ConnectionState.Listen, connections[3].State);
        Assert.Equal(ConnectionProtocol.Udp, connections[4].Protocol);
        Assert.Null(connections[4].RemoteAddress);
    }

    [Theory]
    [InlineData(ConnectionSortKey.ProcessId)]
    [InlineData(ConnectionSortKey.Process)]
    [InlineData(ConnectionSortKey.Protocol)]
    [InlineData(ConnectionSortKey.State)]
    [InlineData(ConnectionSortKey.Local)]
    [InlineData(ConnectionSortKey.Remote)]
    public void Sort_AlwaysDeterministic(ConnectionSortKey key)
    {
        var connections = new List<ConnectionInfo>
        {
            Tcp("10.0.0.1", 443, "8.8.8.8", 53, ConnectionState.Established, 2, "b"),
            Tcp("10.0.0.1", 80, "8.8.4.4", 443, ConnectionState.Listen, 1, "a"),
            Udp("10.0.0.1", 5353, null, null, 3, "c")
        };

        var first = connections.OrderBy(c => c, ConnectionSort.Create(key)).ToList();
        var second = connections.OrderBy(c => c, ConnectionSort.Create(key)).ToList();

        Assert.Equal(first.Select(c => Endpoint(c)), second.Select(c => Endpoint(c)));
    }

    private static string Endpoint(ConnectionInfo connection) =>
        $"{connection.LocalAddress}:{connection.LocalPort}/{(connection.RemoteAddress is null ? "-" : connection.RemoteAddress)}:{connection.RemotePort?.ToString() ?? "-"}/pid{connection.ProcessId}/{connection.State}";
}