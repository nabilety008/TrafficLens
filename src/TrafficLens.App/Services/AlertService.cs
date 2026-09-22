using Microsoft.Extensions.Logging;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Alerts;
using TrafficLens.Core.History;
using TrafficLens.Core.Models;
using TrafficLens.Network.Aggregation;

namespace TrafficLens.App.Services;

/// <summary>
/// Owns the alert engine and its pipeline subscriptions. Speed rules are fed by
/// the collector's aggregate rate (same ADR-009/010 source as the dashboard);
/// usage rules are fed by the history service's in-memory snapshots on each
/// <see cref="ITrafficHistoryService.HistoryChanged"/> (low-frequency refresh, no
/// SQLite queries here). Every raised alert is appended to the bounded in-memory
/// recent list and exposed through <see cref="AlertRaised"/>; nothing here touches
/// the UI or the tray, so evaluation stays thread-agnostic and cannot deadlock.
/// </summary>
public sealed class AlertService : IAlertService
{
    private readonly INetworkTrafficCollector _collector;
    private readonly INetworkAdapterProvider _adapterProvider;
    private readonly ITrafficHistoryService _history;
    private readonly ISettingsService _settings;
    private readonly ILogger<AlertService> _logger;

    private readonly AlertEngine _engine;
    private readonly AlertHistoryBuffer _buffer = new();
    private bool _historyWasUnavailable;
    private bool _disposed;

    public AlertService(
        INetworkTrafficCollector collector,
        INetworkAdapterProvider adapterProvider,
        ITrafficHistoryService history,
        ISettingsService settings,
        ILogger<AlertService> logger)
    {
        _collector = collector;
        _adapterProvider = adapterProvider;
        _history = history;
        _settings = settings;
        _logger = logger;

        var config = AlertSettings.Load(settings);
        CurrentConfig = config;
        _engine = new AlertEngine(
            config,
            TimeZoneInfo.Local,
            () => DateTimeOffset.UtcNow);
        _engine.RestoreTriggeredDates(AlertSettings.LoadTriggeredDates(settings));

        _collector.SpeedSampleReady += OnSpeedSampleReady;
        _collector.NetworkChanged += OnNetworkChanged;
        _history.HistoryChanged += OnHistoryChanged;
    }

    public event EventHandler<AlertRaisedEventArgs>? AlertRaised;

    public event EventHandler? ConfigChanged;

    public IReadOnlyList<AlertEvent> RecentAlerts => _buffer.Latest();

    public AlertConfig CurrentConfig { get; private set; }

    public void RefreshConfig()
    {
        CurrentConfig = AlertSettings.Load(_settings);
        _engine.UpdateConfig(CurrentConfig);
        _logger.LogInformation("Alert configuration refreshed from settings");
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _collector.SpeedSampleReady -= OnSpeedSampleReady;
        _collector.NetworkChanged -= OnNetworkChanged;
        _history.HistoryChanged -= OnHistoryChanged;
    }

    private void OnSpeedSampleReady(object? sender, NetworkSpeedSample sample)
    {
        var adapters = _adapterProvider.GetAdapters();
        var aggregate = NetworkTrafficAggregator.AggregateRates(
            _collector.GetCurrentSamples(),
            adapters);

        if (aggregate is null)
        {
            return;
        }

        foreach (var signal in _engine.EvaluateSpeed(
            aggregate.DownloadBytesPerSecond,
            aggregate.UploadBytesPerSecond))
        {
            Raise(signal);
        }
    }

    private void OnNetworkChanged(object? sender, EventArgs e)
    {
        // Adapters changed; the next speed sample aggregates against the fresh
        // adapter set. Daily rules are unaffected by topology.
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        var snapshot = _history.GetSnapshot();
        if (snapshot.IsAvailable)
        {
            _historyWasUnavailable = false;
            var signals = _engine.EvaluateDaily(snapshot);
            foreach (var signal in signals)
            {
                Raise(signal);
            }

            return;
        }

        if (!_historyWasUnavailable)
        {
            _historyWasUnavailable = true;
            _logger.LogError(
                "Traffic history unavailable; daily usage alerts suspended (speed alerts continue): {Error}",
                snapshot.Error);
        }
    }

    private void Raise(AlertSignal signal)
    {
        _logger.LogInformation(
            "Alert triggered: {Type} value={Value} threshold={Threshold}",
            signal.Type,
            signal.Value,
            signal.Threshold);

        var alert = new AlertEvent(signal.Type, signal.Value, signal.Threshold, DateTimeOffset.UtcNow);
        _buffer.Add(alert);

        if (signal.Type.IsDailyUsageRule())
        {
            AlertSettings.SaveTriggeredDates(_settings, _engine.TriggeredDailyDates());
        }

        AlertRaised?.Invoke(this, new AlertRaisedEventArgs(alert));
    }
}