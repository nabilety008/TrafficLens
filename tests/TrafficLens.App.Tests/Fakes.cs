using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Models;

namespace TrafficLens.App.Tests;

internal sealed class FakeCollector : INetworkTrafficCollector
{
    private readonly List<NetworkSpeedSample> _samples = new();

    #pragma warning disable CS0067
    public event EventHandler<NetworkCounterSample>? CounterSampleReady;
#pragma warning restore CS0067

    public event EventHandler<NetworkSpeedSample>? SpeedSampleReady;

    public event EventHandler? NetworkChanged;

    public IReadOnlyList<NetworkCounterSample> GetCurrentCounterSamples() => Array.Empty<NetworkCounterSample>();

    public IReadOnlyList<NetworkSpeedSample> GetCurrentSamples() => _samples;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;

    public void Dispose()
    {
    }

    public void SetSamples(params NetworkSpeedSample[] samples)
    {
        _samples.Clear();
        _samples.AddRange(samples);
        foreach (var sample in samples)
        {
            SpeedSampleReady?.Invoke(this, sample);
        }
    }

    public void RaiseSample(NetworkSpeedSample sample)
    {
        var existing = _samples.FindIndex(s => s.AdapterId.Equals(sample.AdapterId, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            _samples[existing] = sample;
        }
        else
        {
            _samples.Add(sample);
        }

        SpeedSampleReady?.Invoke(this, sample);
    }

    public void RaiseNetworkChanged() => NetworkChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class FakeAdapterProvider : INetworkAdapterProvider
{
    private IReadOnlyList<NetworkAdapterInfo> _adapters = Array.Empty<NetworkAdapterInfo>();

    public event EventHandler? AdaptersChanged;

    public IReadOnlyList<NetworkAdapterInfo> GetAdapters() => _adapters;

    public NetworkAdapterInfo? GetDefaultAdapter() => _adapters.FirstOrDefault(a => a.IsDefault);

    public void SetAdapters(IReadOnlyList<NetworkAdapterInfo> adapters)
    {
        _adapters = adapters;
        AdaptersChanged?.Invoke(this, EventArgs.Empty);
    }
}