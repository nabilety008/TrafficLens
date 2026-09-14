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

    /// <summary>
    /// Description-aware classification. VPN drivers (OpenVPN TAP/DCO, WireGuard,
    /// Wintun) often register unusual interface types (e.g. HighPerformanceSerialBus),
    /// so their driver descriptions are the strongest signal. Virtual switch nics
    /// (Hyper-V, VMware) follow the same rule.
    /// </summary>
    public static NetworkAdapterKind Map(NetworkInterfaceType type, string? description)
    {
        if (HasVirtualOrTunnelDescription(description))
        {
            return HasTunnelDescription(description)
                ? NetworkAdapterKind.Tunnel
                : NetworkAdapterKind.Virtual;
        }

        return Map(type);
    }

    public static bool HasVirtualOrTunnelDescription(string? description)
    {
        return HasTunnelDescription(description) || HasVirtualDescription(description);
    }

    private static bool HasTunnelDescription(string? description)
    {
        if (string.IsNullOrEmpty(description))
        {
            return false;
        }

        var text = description.ToLowerInvariant();
        return text.Contains("tap-windows", StringComparison.Ordinal) ||
               text.Contains("openvpn", StringComparison.Ordinal) ||
               text.Contains("wintun", StringComparison.Ordinal) ||
               text.Contains("wireguard", StringComparison.Ordinal);
    }

    private static bool HasVirtualDescription(string? description)
    {
        if (string.IsNullOrEmpty(description))
        {
            return false;
        }

        var text = description.ToLowerInvariant();
        return text.Contains("virtual", StringComparison.Ordinal) ||
               text.Contains("vmware", StringComparison.Ordinal) ||
               text.Contains("hyper-v", StringComparison.Ordinal) ||
               text.Contains("vethernet", StringComparison.Ordinal) ||
               text.Contains("virtualbox", StringComparison.Ordinal);
    }
}