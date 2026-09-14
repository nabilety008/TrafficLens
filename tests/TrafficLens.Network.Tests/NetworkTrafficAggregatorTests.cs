using System.Net.NetworkInformation;
using TrafficLens.Core.Models;
using TrafficLens.Network.Aggregation;
using TrafficLens.Network.Adapters;

namespace TrafficLens.Network.Tests;

public sealed class NetworkTrafficAggregatorTests
{
    [Fact]
    public void Sum_AddsCountersAcrossAdapters()
    {
        var t = new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);
        var samples = new List<NetworkCounterSample>
        {
            new("a", "Ethernet", 1000, 500, t),
            new("b", "Wi-Fi", 2000, 700, t.AddSeconds(1))
        };

        var total = NetworkTrafficAggregator.Sum(samples);

        Assert.Equal(3000, total.ReceivedBytes);
        Assert.Equal(1200, total.SentBytes);
        Assert.Equal(4200, total.TotalBytes);
        Assert.Equal(t.AddSeconds(1), total.Timestamp);
    }

    [Fact]
    public void Sum_EmptySamples_ReturnsZeroTotals()
    {
        var total = NetworkTrafficAggregator.Sum(Array.Empty<NetworkCounterSample>());

        Assert.Equal(0, total.ReceivedBytes);
        Assert.Equal(0, total.SentBytes);
    }

    [Fact]
    public void GetNonOverlappingAdapters_ExcludesTunnels_WhenIncludeTunnelsFalse()
    {
        var t = new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);
        var samples = new List<NetworkCounterSample>
        {
            new("eth0", "Ethernet", 100, 200, t),
            new("wg0", "WireGuard", 300, 400, t),
            new("vmnic", "VMware", 50, 60, t)
        };

        var adapters = new List<NetworkAdapterInfo>
        {
            new("eth0", "Ethernet", "desc", "", NetworkAdapterKind.Ethernet, true, false),
            new("wg0", "WireGuard", "desc", "", NetworkAdapterKind.Tunnel, true, false),
            new("vmnic", "VMware", "desc", "", NetworkAdapterKind.Virtual, true, false)
        };

        var selected = NetworkTrafficAggregator.GetNonOverlappingAdapters(samples, adapters);

        // Tunnel is excluded (double-counts the same payload), virtual nic is kept.
        Assert.Contains(selected, s => s.AdapterId == "eth0");
        Assert.Contains(selected, s => s.AdapterId == "vmnic");
        Assert.DoesNotContain(selected, s => s.AdapterId == "wg0");
    }

    [Fact]
    public void GetNonOverlappingAdapters_IncludeTunnelsTrue_KeepsAll()
    {
        var t = DateTime.UtcNow;
        var samples = new List<NetworkCounterSample>
        {
            new("eth0", "Ethernet", 100, 200, t),
            new("wg0", "WireGuard", 300, 400, t)
        };

        var adapters = new List<NetworkAdapterInfo>
        {
            new("eth0", "Ethernet", "desc", "", NetworkAdapterKind.Ethernet, true, false),
            new("wg0", "WireGuard", "desc", "", NetworkAdapterKind.Tunnel, true, false)
        };

        var selected = NetworkTrafficAggregator.GetNonOverlappingAdapters(samples, adapters, includeTunnels: true);

        Assert.Equal(2, selected.Count);
    }

    [Fact]
    public void GetNonOverlappingAdapters_ExcludesDownAdapters()
    {
        var t = DateTime.UtcNow;
        var samples = new List<NetworkCounterSample>
        {
            new("eth0", "Ethernet", 100, 200, t)
        };

        var adapters = new List<NetworkAdapterInfo>
        {
            new("eth0", "Ethernet", "desc", "", NetworkAdapterKind.Ethernet, IsUp: false, false)
        };

        Assert.Empty(NetworkTrafficAggregator.GetNonOverlappingAdapters(samples, adapters));
    }

    [Fact]
    public void GetNonOverlappingAdapters_EmptyWhenOnlyTunnelIsActive()
    {
        // Honest result: a VPN-only host has no non-overlapping physical total.
        var t = DateTime.UtcNow;
        var samples = new List<NetworkCounterSample>
        {
            new("wg0", "WireGuard", 300, 400, t)
        };

        var adapters = new List<NetworkAdapterInfo>
        {
            new("wg0", "WireGuard", "desc", "", NetworkAdapterKind.Tunnel, true, false)
        };

        Assert.Empty(NetworkTrafficAggregator.GetNonOverlappingAdapters(samples, adapters));
    }
}