using TrafficLens.Core.Models;

namespace TrafficLens.Core.Abstractions;

public interface INetworkAdapterProvider
{
    event EventHandler? AdaptersChanged;

    IReadOnlyList<NetworkAdapterInfo> GetAdapters();

    NetworkAdapterInfo? GetDefaultAdapter();
}