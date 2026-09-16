using System.Net;
using System.Net.NetworkInformation;

namespace TrafficLens.Network.Adapters;

/// <summary>
/// Adapter snapshot source backed by the managed <see cref="NetworkInterface"/>
/// API. Snapshots are served through an <see cref="AdapterSnapshotCache"/> so the
/// per-second collectors and the per-sample view models share one enumeration per
/// refresh window instead of each triggering their own (see the cache for the
/// cost rationale). Network address/availability changes invalidate the cache
/// immediately so topology changes surface on the next call.
/// </summary>
public sealed class NetworkInterfaceSource : INetworkInterfaceSource
{
    private readonly AdapterSnapshotCache _cache;

    public NetworkInterfaceSource()
    {
        _cache = new AdapterSnapshotCache(Enumerate);
        NetworkChange.NetworkAddressChanged += (_, _) => _cache.Invalidate();
        NetworkChange.NetworkAvailabilityChanged += (_, _) => _cache.Invalidate();
    }

    public IReadOnlyList<RawAdapterSnapshot> GetAdapters() => _cache.GetSnapshot();

    private static IReadOnlyList<RawAdapterSnapshot> Enumerate()
    {
        var result = new List<RawAdapterSnapshot>();

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            var isUp = networkInterface.OperationalStatus == OperationalStatus.Up;

            var ipProperties = TryGet(() => networkInterface.GetIPProperties());
            var stats = TryGet(() => networkInterface.GetIPv4Statistics());

            var hasGateway = ipProperties?.GatewayAddresses
                .Any(g => g?.Address is { } ip
                          && !ip.Equals(IPAddress.Any)
                          && !ip.Equals(IPAddress.IPv6Any)) ?? false;

            var unicast = ipProperties?.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                ?.Address?.ToString();

            var speed = isUp
                ? TryGet(() => (long?)networkInterface.Speed)
                : null;

            result.Add(new RawAdapterSnapshot(
                Id: networkInterface.Id,
                Name: networkInterface.Name,
                Description: networkInterface.Description,
                PhysicalAddress: networkInterface.GetPhysicalAddress()?.ToString() ?? string.Empty,
                InterfaceType: networkInterface.NetworkInterfaceType,
                IsUp: isUp,
                HasGateway: hasGateway,
                IpAddress: unicast,
                LinkSpeedBitsPerSecond: speed,
                ReceivedBytes: stats?.BytesReceived ?? 0,
                SentBytes: stats?.BytesSent ?? 0));
        }

        return result;
    }

    private static T TryGet<T>(Func<T> getter)
    {
        try
        {
            return getter();
        }
        catch (Exception)
        {
            // A single interface must never break enumeration (e.g. no IPv4 support).
            return default!;
        }
    }
}