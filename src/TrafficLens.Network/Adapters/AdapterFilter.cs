using System.Net.NetworkInformation;
using TrafficLens.Core.Models;

namespace TrafficLens.Network.Adapters;

public static class AdapterFilter
{
    public static bool IsMonitored(RawAdapterSnapshot snapshot)
    {
        return snapshot.InterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Unknown);
    }

    public static NetworkAdapterInfo ToAdapterInfo(RawAdapterSnapshot snapshot)
    {
        return new NetworkAdapterInfo(
            snapshot.Id,
            snapshot.Name,
            snapshot.Description,
            snapshot.PhysicalAddress,
            NetworkAdapterKindMapper.Map(snapshot.InterfaceType),
            snapshot.IsUp,
            IsDefault: false)
        {
            IpAddress = snapshot.IpAddress,
            LinkSpeedBitsPerSecond = snapshot.LinkSpeedBitsPerSecond
        };
    }
}