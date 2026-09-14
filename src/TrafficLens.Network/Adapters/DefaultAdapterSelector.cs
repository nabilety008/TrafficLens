using TrafficLens.Core.Models;

namespace TrafficLens.Network.Adapters;

public static class DefaultAdapterSelector
{
    public const bool PreferNonTunnel = true;

    public static RawAdapterSnapshot? Select(
        IReadOnlyList<RawAdapterSnapshot> snapshots,
        bool preferNonTunnel = PreferNonTunnel)
    {
        var candidates = snapshots
            .Where(AdapterFilter.IsMonitored)
            .Where(s => s.IsUp)
            .ToList();

        // An adapter with a gateway address is the strongest signal that Windows uses
        // it to reach external networks. Fall back to any up adapter otherwise.
        var withGateway = candidates.Where(s => s.HasGateway).ToList();
        var pool = withGateway.Count > 0 ? withGateway : candidates;

        if (preferNonTunnel)
        {
            var preferred = pool.FirstOrDefault(s =>
            {
                var kind = NetworkAdapterKindMapper.Map(s.InterfaceType, s.Description);
                return kind is not (NetworkAdapterKind.Tunnel or NetworkAdapterKind.Virtual);
            });

            if (preferred is not null)
            {
                return preferred;
            }
        }

        return pool.FirstOrDefault();
    }
}