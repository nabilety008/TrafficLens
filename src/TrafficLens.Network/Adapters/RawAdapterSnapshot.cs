using System.Net.NetworkInformation;

namespace TrafficLens.Network.Adapters;

public sealed record RawAdapterSnapshot(
    string Id,
    string Name,
    string Description,
    string PhysicalAddress,
    NetworkInterfaceType InterfaceType,
    bool IsUp,
    bool HasGateway,
    string? IpAddress,
    long? LinkSpeedBitsPerSecond,
    long ReceivedBytes,
    long SentBytes)
{
    public static RawAdapterSnapshot Empty(NetworkInterface ni) =>
        new(ni.Id, ni.Name, ni.Description, string.Empty, ni.NetworkInterfaceType,
            false, false, null, null, 0, 0);
}