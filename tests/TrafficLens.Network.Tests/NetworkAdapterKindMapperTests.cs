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
    public void Map_ReturnsExpectedKind(NetworkInterfaceType type, NetworkAdapterKind expected)
    {
        Assert.Equal(expected, NetworkAdapterKindMapper.Map(type));
    }
}