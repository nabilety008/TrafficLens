using TrafficLens.Core.Models;

namespace TrafficLens.Network.Aggregation;

public sealed record NetworkTotals(long ReceivedBytes, long SentBytes, DateTime Timestamp)
{
    public long TotalBytes => ReceivedBytes + SentBytes;
}

public static class NetworkTrafficAggregator
{
    /// <summary>
    /// Sums cumulative counters. Only meaningful when the caller passes a set of
    /// adapters that do not carry the same traffic (see <see cref="GetNonOverlappingAdapters"/>).
    /// </summary>
    public static NetworkTotals Sum(IReadOnlyList<NetworkCounterSample> samples)
    {
        if (samples.Count == 0)
        {
            return new NetworkTotals(0, 0, DateTimeOffset.UtcNow.UtcDateTime);
        }

        return new NetworkTotals(
            samples.Sum(s => Math.Max(s.ReceivedBytes, 0)),
            samples.Sum(s => Math.Max(s.SentBytes, 0)),
            samples.Max(s => s.Timestamp));
    }

    /// <summary>
    /// Selects adapters whose counters should be combined into a "system total".
    ///
    /// Exact aggregation behavior:
    /// - Physical (Ethernet/Wireless) and virtual-nic adapters (Hyper-V, VMware, VPN
    ///   tunnel endpoint nics) are independent counter sources and MAY be summed.
    /// - Tunnel adapters (WireGuard/OpenVPN-style) transport the same payload the
    ///   underlying physical adapter already counts, so summing them with the
    ///   physical link over-counts. By default they are excluded to avoid
    ///   double-counting.
    /// - Adapters that are down report no traffic and are excluded.
    /// - If the system's only active connection is a tunnel (e.g. a VPN-only laptop),
    ///   callers must include tunnels explicitly; this policy then yields nothing,
    ///   which is the honest result rather than a fabricated number.
    /// </summary>
    public static IReadOnlyList<NetworkCounterSample> GetNonOverlappingAdapters(
        IReadOnlyList<NetworkCounterSample> samples,
        IReadOnlyList<NetworkAdapterInfo> adapters,
        bool includeTunnels = false)
    {
        var up = adapters
            .Where(a => a.IsUp)
            .ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);

        return samples
            .Where(s => up.ContainsKey(s.AdapterId))
            .Where(s => includeTunnels ||
                        up[s.AdapterId].Kind is not NetworkAdapterKind.Tunnel)
            .ToList();
    }
}