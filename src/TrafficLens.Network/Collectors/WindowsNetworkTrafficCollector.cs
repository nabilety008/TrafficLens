using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.NetworkInformation;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Models;
using TrafficLens.Network.Adapters;
using TrafficLens.Network.Calculation;

namespace TrafficLens.Network.Collectors;

public sealed class WindowsNetworkTrafficCollector : INetworkTrafficCollector
{
    private readonly INetworkInterfaceSource _source;
    private readonly ILogger<WindowsNetworkTrafficCollector> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly object _sync = new();

    private IReadOnlyDictionary<string, NetworkCounterSample> _currentCounters =
        new Dictionary<string, NetworkCounterSample>(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<NetworkSpeedSample> _currentRates = Array.Empty<NetworkSpeedSample>();

    private IReadOnlyList<NetworkAdapterInfo> _currentAdapters = Array.Empty<NetworkAdapterInfo>();
    private RawAdapterSnapshot? _defaultAdapterSnapshot;
    private IReadOnlySet<string> _lastAdapterSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly SpeedRateTracker _rateTracker = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public WindowsNetworkTrafficCollector(
        INetworkInterfaceSource source,
        ILogger<WindowsNetworkTrafficCollector> logger,
        TimeSpan? pollInterval = null)
    {
        _source = source;
        _logger = logger;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
    }

    public event EventHandler<NetworkCounterSample>? CounterSampleReady;

    public event EventHandler<NetworkSpeedSample>? SpeedSampleReady;

    public event EventHandler? NetworkChanged;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_loopTask is not null && !_loopTask.IsCompleted)
            {
                return Task.CompletedTask;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loopTask = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);
        }

        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;

        _logger.LogInformation("Network traffic collector started (poll interval {Interval})", _pollInterval);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;

        CancellationTokenSource? cts;
        Task? loop;
        lock (_sync)
        {
            cts = _cts;
            loop = _loopTask;
            _cts = null;
        }

        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        cts.Dispose();
        _logger.LogInformation("Network traffic collector stopped");
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    public IReadOnlyList<NetworkCounterSample> GetCurrentCounterSamples()
    {
        lock (_sync)
        {
            return _currentCounters.Values.OrderBy(s => s.AdapterName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public IReadOnlyList<NetworkSpeedSample> GetCurrentSamples()
    {
        lock (_sync)
        {
            return _currentRates.ToArray();
        }
    }

    public NetworkAdapterInfo[] GetCurrentAdapters()
    {
        lock (_sync)
        {
            return _currentAdapters.ToArray();
        }
    }

    public RawAdapterSnapshot? GetDefaultAdapterSnapshot()
    {
        lock (_sync)
        {
            return _defaultAdapterSnapshot;
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyDictionary<string, NetworkCounterSample>? newCounters = null;
            IReadOnlyList<NetworkAdapterInfo>? newAdapters = null;
            RawAdapterSnapshot? newDefault = null;

            try
            {
                (newCounters, newAdapters, newDefault) = Collect();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Adapter collection failed; keeping previous counters");
            }

            if (newCounters is not null)
            {
                bool setChanged;
                var nowTicks = Stopwatch.GetTimestamp();
                lock (_sync)
                {
                    setChanged = !_lastAdapterSet.SetEquals(newCounters.Keys);
                    _currentCounters = newCounters;
                    _currentAdapters = newAdapters ?? _currentAdapters;
                    _defaultAdapterSnapshot = newDefault;
                    _lastAdapterSet = new HashSet<string>(newCounters.Keys, StringComparer.OrdinalIgnoreCase);
                }

                var newRates = _rateTracker.Track(newCounters, nowTicks, Stopwatch.Frequency);
                lock (_sync)
                {
                    _currentRates = newRates;
                }

                if (setChanged)
                {
                    _logger.LogInformation("Network adapter set changed ({Count} adapters)", newCounters.Count);
                    NetworkChanged?.Invoke(this, EventArgs.Empty);
                }

                foreach (var sample in newCounters.Values)
                {
                    CounterSampleReady?.Invoke(this, sample);
                }

                foreach (var sample in newRates)
                {
                    SpeedSampleReady?.Invoke(this, sample);
                }
            }

            try
            {
                await Task.Delay(_pollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private (IReadOnlyDictionary<string, NetworkCounterSample> Counters,
        IReadOnlyList<NetworkAdapterInfo> Adapters,
        RawAdapterSnapshot? Default) Collect()
    {
        var snapshots = _source.GetAdapters();

        var adapters = snapshots
            .Where(AdapterFilter.IsMonitored)
            .Select(AdapterFilter.ToAdapterInfo)
            .ToList();

        var counters = new Dictionary<string, NetworkCounterSample>(StringComparer.OrdinalIgnoreCase);
        var previous = GetPreviousCounters();

        foreach (var snapshot in snapshots.Where(AdapterFilter.IsMonitored).Where(s => s.IsUp))
        {
            var id = snapshot.Id;
            var sample = new NetworkCounterSample(
                id,
                snapshot.Name,
                snapshot.ReceivedBytes,
                snapshot.SentBytes,
                DateTime.UtcNow);

            if (previous.TryGetValue(id, out var prior))
            {
                if (sample.ReceivedBytes < prior.ReceivedBytes || sample.SentBytes < prior.SentBytes)
                {
                    _logger.LogWarning(
                        "Counter reset/wrap detected on adapter {Adapter}; re-baselining",
                        snapshot.Name);
                }
            }

            counters[id] = sample;
        }

        var adaptersWithDefaultFlag = MarkDefaultAdapted(adapters, snapshots);
        var defaultSnapshot = DefaultAdapterSelector.Select(snapshots);

        return (counters, adaptersWithDefaultFlag, defaultSnapshot);
    }

    private Dictionary<string, NetworkCounterSample> GetPreviousCounters()
    {
        lock (_sync)
        {
            return new Dictionary<string, NetworkCounterSample>(_currentCounters, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyList<NetworkAdapterInfo> MarkDefaultAdapted(
        IReadOnlyList<NetworkAdapterInfo> adapters,
        IReadOnlyList<RawAdapterSnapshot> snapshots)
    {
        var defaultId = DefaultAdapterSelector.Select(snapshots)?.Id;
        if (defaultId is null)
        {
            return adapters;
        }

        return adapters
            .Select(a => a.Id.Equals(defaultId, StringComparison.OrdinalIgnoreCase)
                ? a with { IsDefault = true }
                : a)
            .ToList();
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        var cts = _cts;
        if (cts is null || cts.IsCancellationRequested)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Windows reported a network address change; re-enumerating adapters");
            var (counters, adapters, defaultSnapshot) = Collect();

            lock (_sync)
            {
                _currentCounters = counters;
                _currentAdapters = adapters;
                _defaultAdapterSnapshot = defaultSnapshot;
            }

            NetworkChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-enumerate adapters after network change");
        }
    }
}