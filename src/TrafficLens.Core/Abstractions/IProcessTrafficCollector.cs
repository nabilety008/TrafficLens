using TrafficLens.Core.Models;

namespace TrafficLens.Core.Abstractions;

public interface IProcessTrafficCollector : IDisposable
{
    ProcessTrafficCollectorStatus Status { get; }

    string? LastError { get; }

    event EventHandler<IReadOnlyList<ProcessTrafficSample>>? SamplesReady;

    IReadOnlyList<ProcessTrafficSample> GetCurrentSamples();

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync();
}