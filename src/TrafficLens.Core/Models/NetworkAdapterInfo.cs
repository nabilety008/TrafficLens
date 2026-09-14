namespace TrafficLens.Core.Models;

public enum NetworkAdapterKind
{
    Unknown,
    Ethernet,
    Wireless,
    Tunnel,
    Virtual
}

public sealed record NetworkAdapterInfo(
    string Id,
    string Name,
    string Description,
    string PhysicalAddress,
    NetworkAdapterKind Kind,
    bool IsUp,
    bool IsDefault)
{
    public string? IpAddress { get; init; }

    public long? LinkSpeedBitsPerSecond { get; init; }
}