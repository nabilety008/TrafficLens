using TrafficLens.Core.Models;

namespace TrafficLens.Core.Abstractions;

public interface IProcessTrafficCollector : IDisposable
{
    event EventHandler<IReadOnlyList<ProcessTrafficSample>>? SamplesReady;

    IReadOnlyList<ProcessTrafficSample> GetCurrentSamples();

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync();
}