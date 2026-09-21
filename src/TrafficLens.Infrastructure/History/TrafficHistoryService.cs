using Microsoft.Extensions.Logging;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.History;
using TrafficLens.Core.Models;

namespace TrafficLens.Infrastructure.History;

/// <summary>
/// Records system-wide traffic history by observing the existing collector's
/// cumulative counter samples and periodically flushing compact minute buckets to
/// <see cref="ITrafficHistoryRepository"/>.
///
/// It never starts a second NIC polling loop; it rides the collector's existing
/// ~1s <see cref="INetworkTrafficCollector.CounterSampleReady"/> events via
/// <see cref="TrafficHistoryAccumulator"/> (baseline/delta/re-baseline logic), so
/// counter resets, reconnects and reboots cannot fabricate usage. All database
/// work happens on background threads and is surfaced to the UI through an
/// immutable <see cref="HistorySnapshot"/>.
/// </summary>
public sealed class TrafficHistoryService : ITrafficHistoryService
{
    private static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RawRetention = TimeSpan.FromDays(90);

    private readonly INetworkTrafficCollector _collector;
    private readonly INetworkAdapterProvider _adapterProvider;
    private readonly ITrafficHistoryRepository _repository;
    private readonly ILogger<TrafficHistoryService> _logger;
    private readonly TimeZoneInfo _timeZone;
    private readonly TimeSpan _flushInterval;
    private readonly Func<DateTime> _utcNow;
    private readonly TrafficHistoryAccumulator _accumulator;
    private readonly object _lifecycleGate = new();

    private IReadOnlyDictionary<string, NetworkAdapterKind> _adapterKinds =
        new Dictionary<string, NetworkAdapterKind>(StringComparer.OrdinalIgnoreCase);
    private HistorySnapshot _snapshot = HistorySnapshot.Unavailable(null);
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _started;
    private bool _disposed;

    public TrafficHistoryService(
        INetworkTrafficCollector collector,
        INetworkAdapterProvider adapterProvider,
        ITrafficHistoryRepository repository,
        ILogger<TrafficHistoryService> logger,
        TimeZoneInfo? timeZone = null,
        TimeSpan? flushInterval = null,
        Func<DateTime>? utcNow = null)
    {
        _collector = collector;
        _adapterProvider = adapterProvider;
        _repository = repository;
        _logger = logger;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
        _flushInterval = flushInterval ?? DefaultFlushInterval;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _accumulator = new TrafficHistoryAccumulator();
    }

    public event EventHandler? HistoryChanged;

    public bool IsAvailable => _repository.IsAvailable;

    public string? LastError => _repository.LastError;

    public HistorySnapshot GetSnapshot() => Volatile.Read(ref _snapshot);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lifecycleGate)
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
            _collector.CounterSampleReady += OnCounterSampleReady;
            _adapterProvider.AdaptersChanged += OnAdaptersChanged;
            RefreshAdapterKinds();
            _cts = new CancellationTokenSource();
        }

        var token = _cts!.Token;

        await Task.Run(async () =>
        {
            await _repository.InitializeAsync(token).ConfigureAwait(false);
            await PruneRawSamplesAsync(token).ConfigureAwait(false);
            await RefreshSnapshotAsync(token).ConfigureAwait(false);
        }, token).ConfigureAwait(false);

        _loopTask = Task.Run(() => RunAsync(token), CancellationToken.None);

        if (!_repository.IsAvailable)
        {
            _logger.LogWarning("Traffic history is running without storage: {Error}", _repository.LastError);
        }
        else
        {
            _logger.LogInformation("Traffic history service started");
        }
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_lifecycleGate)
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _collector.CounterSampleReady -= OnCounterSampleReady;
            _adapterProvider.AdaptersChanged -= OnAdaptersChanged;
            cts = _cts;
            loop = _loopTask;
            _cts = null;
            _loopTask = null;
        }

        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

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

        var pending = _accumulator.DrainAll(_utcNow());
        if (pending.Count > 0 && _repository.IsAvailable)
        {
            await _repository
                .AppendBucketsAsync(pending, _timeZone, CancellationToken.None)
                .ConfigureAwait(false);
        }

        await RefreshSnapshotAsync(CancellationToken.None).ConfigureAwait(false);

        cts?.Dispose();
        _logger.LogInformation("Traffic history service stopped");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopAsync().GetAwaiter().GetResult();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_flushInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while flushing traffic history");
            }
        }
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        var buckets = _accumulator.DrainCompleted(_utcNow());
        if (buckets.Count > 0)
        {
            await _repository
                .AppendBucketsAsync(buckets, _timeZone, cancellationToken)
                .ConfigureAwait(false);
        }

        await RefreshSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task PruneRawSamplesAsync(CancellationToken cancellationToken)
    {
        if (!_repository.IsAvailable)
        {
            return;
        }

        var cutoff = _utcNow() - RawRetention;
        var deleted = await _repository
            .PruneRawSamplesBeforeAsync(cutoff, cancellationToken)
            .ConfigureAwait(false);
        if (deleted > 0)
        {
            _logger.LogInformation("Compacted {Count} raw history sample(s) older than {Cutoff:u}", deleted, cutoff);
        }
    }

    private async Task RefreshSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = await BuildSnapshotAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Exchange(ref _snapshot, snapshot);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<HistorySnapshot> BuildSnapshotAsync(CancellationToken cancellationToken)
    {
        if (!_repository.IsAvailable)
        {
            return HistorySnapshot.Unavailable(_repository.LastError);
        }

        try
        {
            var nowUtc = _utcNow();
            var today = HistoryRangeCalculator.LocalDateOf(nowUtc, _timeZone);

            var thirtyDayRange = HistoryRangeCalculator.ToLocalDateRange(HistoryRange.Last30Days, today)!.Value;
            var monthRange = HistoryRangeCalculator.ToLocalDateRange(HistoryRange.ThisMonth, today)!.Value;
            var daily = await _repository
                .QueryDailyAsync(thirtyDayRange.Start, thirtyDayRange.EndExclusive, cancellationToken)
                .ConfigureAwait(false);
            var lifetime = await _repository
                .QueryLifetimeAsync(cancellationToken)
                .ConfigureAwait(false);

            var series = HistoryRangeCalculator.BuildDailySeries(
                thirtyDayRange.Start, thirtyDayRange.EndExclusive, daily);

            var dayStartUtc = HourlyHistoryBuilder.MidnightUtc(today, _timeZone);
            IReadOnlyList<HourlyUsagePoint> todayHourly = Array.Empty<HourlyUsagePoint>();
            double dayFraction = 0;
            if (nowUtc > dayStartUtc)
            {
                var samples = await _repository
                    .QuerySamplesAsync(dayStartUtc, nowUtc, cancellationToken)
                    .ConfigureAwait(false);
                todayHourly = HourlyHistoryBuilder.Build(nowUtc, _timeZone, samples);
                var nextDayUtc = dayStartUtc.AddDays(1);
                var spanTicks = (nextDayUtc - dayStartUtc).Ticks;
                dayFraction = spanTicks > 0
                    ? Math.Clamp((double)(nowUtc.Ticks - dayStartUtc.Ticks) / spanTicks, 0, 1)
                    : 0;
            }

            return new HistorySnapshot(
                true,
                _repository.LastError,
                HistoryRangeCalculator.SumDaily(daily, today, today.AddDays(1)),
                HistoryRangeCalculator.SumDaily(daily, today.AddDays(-1), today),
                HistoryRangeCalculator.SumDaily(daily, today.AddDays(-6), today.AddDays(1)),
                HistoryRangeCalculator.SumDaily(daily, thirtyDayRange.Start, thirtyDayRange.EndExclusive),
                lifetime,
                series,
                todayHourly,
                HistoryRangeCalculator.SumDaily(daily, monthRange.Start, monthRange.EndExclusive),
                dayFraction);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build traffic history snapshot");
            return HistorySnapshot.Unavailable(ex.Message);
        }
    }

    private void OnCounterSampleReady(object? sender, NetworkCounterSample sample)
    {
        var kind = _adapterKinds.TryGetValue(sample.AdapterId, out var value)
            ? value
            : (NetworkAdapterKind?)null;
        _accumulator.Ingest(sample, kind);
    }

    private void OnAdaptersChanged(object? sender, EventArgs e) => RefreshAdapterKinds();

    private void RefreshAdapterKinds()
    {
        try
        {
            var map = new Dictionary<string, NetworkAdapterKind>(StringComparer.OrdinalIgnoreCase);
            foreach (var adapter in _adapterProvider.GetAdapters())
            {
                map[adapter.Id] = adapter.Kind;
            }

            _adapterKinds = map;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh adapter kinds for traffic history");
        }
    }
}
