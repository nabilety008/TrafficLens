using System.Net.NetworkInformation;
using TrafficLens.Core.Models;
using TrafficLens.Network.Adapters;

namespace TrafficLens.Network.Tests;

public sealed class AdapterFilterTests
{
    [Fact]
    public void IsMonitored_ExcludesLoopback()
    {
        var snap = Snap(NetworkInterfaceType.Loopback);
        Assert.False(AdapterFilter.IsMonitored(snap));
    }

    [Fact]
    public void IsMonitored_ExcludesUnknown()
    {
        var snap = Snap(NetworkInterfaceType.Unknown);
        Assert.False(AdapterFilter.IsMonitored(snap));
    }

    [Theory]
    [InlineData(NetworkInterfaceType.Ethernet)]
    [InlineData(NetworkInterfaceType.Wireless80211)]
    [InlineData(NetworkInterfaceType.Tunnel)]
    [InlineData(NetworkInterfaceType.Wwanpp)]
    public void IsMonitored_IncludesRealAndVirtualTypes(NetworkInterfaceType type)
    {
        Assert.True(AdapterFilter.IsMonitored(Snap(type)));
    }

    [Fact]
    public void ToAdapterInfo_CopiesKeyFields()
    {
        var snap = Snap(NetworkInterfaceType.Wireless80211) with
        {
            Name = "Wi-Fi",
            Description = "Intel Wireless",
            PhysicalAddress = "001122334455",
            IsUp = true,
            HasGateway = true,
            IpAddress = "192.168.1.5",
            LinkSpeedBitsPerSecond = 70_000_000
        };

        var info = AdapterFilter.ToAdapterInfo(snap);

        Assert.Equal(snap.Id, info.Id);
        Assert.Equal("Wi-Fi", info.Name);
        Assert.Equal("Intel Wireless", info.Description);
        Assert.Equal("001122334455", info.PhysicalAddress);
        Assert.Equal(NetworkAdapterKind.Wireless, info.Kind);
        Assert.True(info.IsUp);
        Assert.Equal("192.168.1.5", info.IpAddress);
        Assert.Equal(70_000_000, info.LinkSpeedBitsPerSecond);
    }

    private static RawAdapterSnapshot Snap(NetworkInterfaceType type) =>
        new(Guid.NewGuid().ToString("N"), "a", "b", string.Empty, type,
            false, false, null, null, 0, 0);
}