using System.Net.NetworkInformation;
using TrafficLens.Core.Models;

namespace TrafficLens.Network.Adapters;

public static class AdapterFilter
{
    public static bool IsMonitored(RawAdapterSnapshot snapshot)
    {
        if (snapshot.InterfaceType == NetworkInterfaceType.Loopback)
        {
            return false;
        }

        // Phantom interfaces report Unknown type with no device description;
        // keep them out. Real VPN/virtual devices that happen to report Unknown
        // are identified by their driver description (OpenVPN/WireGuard/Hyper-V).
        if (snapshot.InterfaceType == NetworkInterfaceType.Unknown &&
            !NetworkAdapterKindMapper.HasVirtualOrTunnelDescription(snapshot.Description))
        {
            return false;
        }

        return true;
    }

    public static NetworkAdapterInfo ToAdapterInfo(RawAdapterSnapshot snapshot)
    {
        return new NetworkAdapterInfo(
            snapshot.Id,
            snapshot.Name,
            snapshot.Description,
            snapshot.PhysicalAddress,
            NetworkAdapterKindMapper.Map(snapshot.InterfaceType, snapshot.Description),
            snapshot.IsUp,
            IsDefault: false)
        {
            IpAddress = snapshot.IpAddress,
            LinkSpeedBitsPerSecond = snapshot.LinkSpeedBitsPerSecond
        };
    }
}