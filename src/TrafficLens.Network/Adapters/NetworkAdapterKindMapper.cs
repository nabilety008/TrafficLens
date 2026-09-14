using System.Net.NetworkInformation;
using TrafficLens.Core.Models;

namespace TrafficLens.Network.Adapters;

public static class NetworkAdapterKindMapper
{
    public static NetworkAdapterKind Map(NetworkInterfaceType type)
    {
        return type switch
        {
            NetworkInterfaceType.Ethernet or
            NetworkInterfaceType.FastEthernetT or
            NetworkInterfaceType.FastEthernetFx or
            NetworkInterfaceType.GigabitEthernet => NetworkAdapterKind.Ethernet,

            NetworkInterfaceType.Wireless80211 => NetworkAdapterKind.Wireless,

            NetworkInterfaceType.Tunnel => NetworkAdapterKind.Tunnel,

            NetworkInterfaceType.Ppp or
            NetworkInterfaceType.Wwanpp or
            NetworkInterfaceType.Wwanpp2 or
            NetworkInterfaceType.GenericModem => NetworkAdapterKind.Virtual,

            NetworkInterfaceType.Loopback or
            NetworkInterfaceType.Unknown => NetworkAdapterKind.Unknown,

            _ => NetworkAdapterKind.Unknown
        };
    }
}