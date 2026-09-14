using System.Net.NetworkInformation;
using TrafficLens.Network.Adapters;

namespace TrafficLens.Network.Tests;

public sealed class DefaultAdapterSelectorTests
{
    [Fact]
    public void Select_PrefersUpAdapterWithGateway()
    {
        var up = Snap("eth", NetworkInterfaceType.Ethernet, isUp: true, hasGateway: true);
        var down = Snap("wifi", NetworkInterfaceType.Wireless80211, isUp: false, hasGateway: true);

        var chosen = DefaultAdapterSelector.Select([down, up]);

        Assert.Equal("eth", chosen?.Id);
    }

    [Fact]
    public void Select_PrefersNonTunnelOverTunnelWhenBothHaveGateway()
    {
        var tunnel = Snap("wg0", NetworkInterfaceType.Tunnel, isUp: true, hasGateway: true);
        var likelyPhysical = Snap("eth0", NetworkInterfaceType.Ethernet, isUp: true, hasGateway: true);

        var chosen = DefaultAdapterSelector.Select([tunnel, likelyPhysical]);

        // Avoids choosing a VPN tunnel as "the" default adapter when a physical
        // candidate is available.
        Assert.Equal("eth0", chosen?.Id);
    }

    [Fact]
    public void Select_FallsBackToUpAdapterWithoutGateway()
    {
        var noGateway = Snap("lone", NetworkInterfaceType.Ethernet, isUp: true, hasGateway: false);

        var chosen = DefaultAdapterSelector.Select([noGateway]);

        Assert.Equal("lone", chosen?.Id);
    }

    [Fact]
    public void Select_ReturnsNullWhenNothingIsUp()
    {
        var down = Snap("down1", NetworkInterfaceType.Ethernet, isUp: false, hasGateway: true);

        Assert.Null(DefaultAdapterSelector.Select([down]));
    }

    [Fact]
    public void Select_IgnoresLoopback()
    {
        var loop = Snap("lo", NetworkInterfaceType.Loopback, isUp: true, hasGateway: true);
        var ether = Snap("eth", NetworkInterfaceType.Ethernet, isUp: true, hasGateway: true);

        Assert.Equal("eth", DefaultAdapterSelector.Select([loop, ether])?.Id);
    }

    [Fact]
    public void Select_PrefersPhysicalOverTapAdapterWithSameTypeSignature()
    {
        var tap = Snap("tun", NetworkInterfaceType.HighPerformanceSerialBus, isUp: true, hasGateway: true) with
        {
            Description = "TAP-Windows Adapter V9 for OpenVPN Connect"
        };
        var physical = Snap("eth0", NetworkInterfaceType.Ethernet, isUp: true, hasGateway: true);

        var chosen = DefaultAdapterSelector.Select([tap, physical]);

        Assert.Equal("eth0", chosen?.Id);
    }

    private static RawAdapterSnapshot Snap(string id, NetworkInterfaceType type, bool isUp, bool hasGateway) =>
        new(id, id + "-name", "desc", string.Empty, type, isUp, hasGateway,
            hasGateway ? "10.0.0.2" : null, isUp ? 1_000_000_000L : null, 100, 50);
}