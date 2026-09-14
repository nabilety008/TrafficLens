using System.Net.NetworkInformation;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Models;

namespace TrafficLens.Network.Adapters;

public sealed class WindowsNetworkAdapterProvider : INetworkAdapterProvider
{
    private readonly INetworkInterfaceSource _source;

    public WindowsNetworkAdapterProvider(INetworkInterfaceSource source)
    {
        _source = source;
        NetworkChange.NetworkAddressChanged += (_, _) => AdaptersChanged?.Invoke(this, EventArgs.Empty);
        NetworkChange.NetworkAvailabilityChanged += (_, _) => AdaptersChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? AdaptersChanged;

    public IReadOnlyList<NetworkAdapterInfo> GetAdapters()
    {
        var snapshots = _source.GetAdapters()
            .Where(AdapterFilter.IsMonitored)
            .ToList();

        var defaultId = DefaultAdapterSelector.Select(snapshots)?.Id;

        return snapshots
            .Select(AdapterFilter.ToAdapterInfo)
            .Select(a => a.Id.Equals(defaultId, StringComparison.OrdinalIgnoreCase)
                ? a with { IsDefault = true }
                : a)
            .ToList();
    }

    public NetworkAdapterInfo? GetDefaultAdapter()
    {
        var defaultSnapshot = DefaultAdapterSelector.Select(_source.GetAdapters());
        return defaultSnapshot is null ? null : AdapterFilter.ToAdapterInfo(defaultSnapshot);
    }
}