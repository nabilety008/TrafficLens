using TrafficLens.Core.Models;

namespace TrafficLens.Core.Abstractions;

public interface INetworkTrafficCollector : IDisposable
{
    event EventHandler<NetworkCounterSample>? CounterSampleReady;

    event EventHandler<NetworkSpeedSample>? SpeedSampleReady;

    event EventHandler? NetworkChanged;

    IReadOnlyList<NetworkCounterSample> GetCurrentCounterSamples();

    IReadOnlyList<NetworkSpeedSample> GetCurrentSamples();

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync();
}