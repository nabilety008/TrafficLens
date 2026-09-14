namespace TrafficLens.Network.Adapters;

public interface INetworkInterfaceSource
{
    IReadOnlyList<RawAdapterSnapshot> GetAdapters();
}