using System.Net.NetworkInformation;
using TrafficLens.Core.Models;
using TrafficLens.Network.Adapters;

namespace TrafficLens.Network.Tests;

public sealed class NetworkAdapterKindMapperTests
{
    [Theory]
    [InlineData(NetworkInterfaceType.Ethernet, NetworkAdapterKind.Ethernet)]
    [InlineData(NetworkInterfaceType.FastEthernetT, NetworkAdapterKind.Ethernet)]
    [InlineData(NetworkInterfaceType.FastEthernetFx, NetworkAdapterKind.Ethernet)]
    [InlineData(NetworkInterfaceType.GigabitEthernet, NetworkAdapterKind.Ethernet)]
    [InlineData(NetworkInterfaceType.Wireless80211, NetworkAdapterKind.Wireless)]
    [InlineData(NetworkInterfaceType.Tunnel, NetworkAdapterKind.Tunnel)]
    [InlineData(NetworkInterfaceType.Ppp, NetworkAdapterKind.Virtual)]
    [InlineData(NetworkInterfaceType.Wwanpp, NetworkAdapterKind.Virtual)]
    [InlineData(NetworkInterfaceType.Wwanpp2, NetworkAdapterKind.Virtual)]
    [InlineData(NetworkInterfaceType.GenericModem, NetworkAdapterKind.Virtual)]
    [InlineData(NetworkInterfaceType.Loopback, NetworkAdapterKind.Unknown)]
    [InlineData(NetworkInterfaceType.Unknown, NetworkAdapterKind.Unknown)]
    public void Map_TypeOnly_ReturnsExpectedKind(NetworkInterfaceType type, NetworkAdapterKind expected)
    {
        Assert.Equal(expected, NetworkAdapterKindMapper.Map(type));
    }

    [Theory]
    [InlineData(NetworkInterfaceType.HighPerformanceSerialBus, "TAP-Windows Adapter V9 for OpenVPN Connect")]
    [InlineData(NetworkInterfaceType.HighPerformanceSerialBus, "OpenVPN Data Channel Offload")]
    [InlineData(NetworkInterfaceType.Ethernet, "WireGuard Tunnel")]
    [InlineData(NetworkInterfaceType.Unknown, "Wintun Userspace Tunnel")]
    public void Map_DescriptionAware_ClassifiesVpnDriversAsTunnel(NetworkInterfaceType type, string description)
    {
        Assert.Equal(NetworkAdapterKind.Tunnel, NetworkAdapterKindMapper.Map(type, description));
    }

    [Theory]
    [InlineData(NetworkInterfaceType.Ethernet, "Hyper-V Virtual Ethernet Adapter")]
    [InlineData(NetworkInterfaceType.Ethernet, "VMware Virtual Ethernet Adapter for VMnet8")]
    [InlineData(NetworkInterfaceType.Wireless80211, "Microsoft Wi-Fi Direct Virtual Adapter")]
    [InlineData(NetworkInterfaceType.Unknown, "VirtualBox Host-Only Ethernet Adapter")]
    public void Map_DescriptionAware_ClassifiesVirtualNicsAsVirtual(NetworkInterfaceType type, string description)
    {
        Assert.Equal(NetworkAdapterKind.Virtual, NetworkAdapterKindMapper.Map(type, description));
    }

    [Theory]
    [InlineData(NetworkInterfaceType.Ethernet, "Bluetooth Device (Personal Area Network)")]
    [InlineData(NetworkInterfaceType.Wireless80211, "Intel(R) Wi-Fi 6 AX201 160MHz")]
    [InlineData(NetworkInterfaceType.Loopback, "Software Loopback Interface 1")]
    public void Map_DescriptionAware_FallsBackToTypeWhenDescriptionIsUnrelated(
        NetworkInterfaceType type, string description)
    {
        Assert.Equal(NetworkAdapterKindMapper.Map(type), NetworkAdapterKindMapper.Map(type, description));
    }
}