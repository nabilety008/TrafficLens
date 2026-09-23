using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.Graph;
using TrafficLens.Core.History;
using TrafficLens.Core.Localization;
using TrafficLens.Core.Models;
using TrafficLens.Core.Selection;
using TrafficLens.Network.Aggregation;

namespace TrafficLens.WinUI.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly INetworkTrafficCollector _collector;
    private readonly INetworkAdapterProvider _adapterProvider;
    private readonly ILocalizationService _localization;
    private readonly ITrafficHistoryService? _historyService;
    private readonly IProcessTrafficCollector? _processCollector;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly IReadOnlyDictionary<NetworkAdapterKind, string> _kindKeys;
    private readonly TrafficSampleBuffer _graphBuffer = new(
        maxRetention: TimeSpan.FromMinutes(5.5),
        capacity: 1320);
    private readonly AdaptiveGraphScale _graphScale = new();

    private IReadOnlyList<NetworkAdapterInfo> _adapters = Array.Empty<NetworkAdapterInfo>();
    private IReadOnlyList<NetworkSpeedSample> _samples = Array.Empty<NetworkSpeedSample>();
    private NetworkSpeedSample? _aggregate;

    private long _downloadBytesPerSecond;
    private long _uploadBytesPerSecond;
    private long _totalBytesPerSecond;
    private bool _hasConnection;

    private IReadOnlyList<TrafficGraphPoint> _graphPoints = Array.Empty<TrafficGraphPoint>();
    private long _graphScaleMax;
    private GraphTimeRange _selectedGraphRange = GraphTimeRange.OneMinute;
    private DateTime _graphReferenceTime = DateTime.MinValue;
    private bool _is30SecondsSelected;
    private bool _is1MinuteSelected = true;
    private bool _is5MinutesSelected;
    private bool _refreshPending;
    private bool _isActive = true;
    private bool _disposed;

    private string _dashboardLabel = string.Empty;
    private string _downloadLabel = string.Empty;
    private string _uploadLabel = string.Empty;
    private string _totalLabel = string.Empty;
    private string _activeAdapterLabel = string.Empty;
    private string _statusLabel = string.Empty;
    private string _connectedLabel = string.Empty;
    private string _disconnectedLabel = string.Empty;
    private string _noActiveConnection = string.Empty;

    private string _downloadText = "0 B/s";
    private string _uploadText = "0 B/s";
    private string _totalText = "0 B/s";
    private string _downloadMbpsText = "0 Mbps";
    private string _uploadMbpsText = "0 Mbps";
    private string _totalMbpsText = "0 Mbps";

    private string _activeAdapterName = string.Empty;
    private string _activeAdapterKindText = string.Empty;
    private string _activeAdapterStatusText = string.Empty;

    private string _graphLiveTrafficLabel = string.Empty;
    private string _graphLast30SecondsLabel = string.Empty;
    private string _graphLast1MinuteLabel = string.Empty;
    private string _graphLast5MinutesLabel = string.Empty;
    private string _graphNowLabel = string.Empty;
    private string _graphDownloadSeriesLabel = string.Empty;
    private string _graphUploadSeriesLabel = string.Empty;
    private string _tunnelAggregateHint = string.Empty;
    private bool _hasTunnelAdapter;

    private string _todayAtGlanceLabel = string.Empty;
    private string _downloadTodayLabel = string.Empty;
    private string _uploadTodayLabel = string.Empty;
    private string _totalTodayLabel = string.Empty;
    private string _todayDownloadText = "0 B";
    private string _todayUploadText = "0 B";
    private string _todayTotalText = "0 B";
    private bool _hasTodayData;

    private string _topAppNowLabel = string.Empty;
    private string _topAppApplicationLabel = string.Empty;
    private string _topAppCurrentLabel = string.Empty;
    private string _topAppNoDataLabel = string.Empty;
    private string _topAppPermissionDeniedLabel = string.Empty;
    private string _topAppUnavailableLabel = string.Empty;
    private string _topAppDownloadLabel = string.Empty;
    private string _topAppUploadLabel = string.Empty;
    private string _topAppName = string.Empty;
    private string _topAppRateText = string.Empty;
    private string _topAppDownloadRateText = string.Empty;
    private string _topAppUploadRateText = string.Empty;
    private string _topAppStatusText = string.Empty;
    private bool _hasTopApp;
    private bool _isTopAppPermissionDenied;
    private bool _isTopAppUnavailable;
    private bool _isTopAppIdle;

    public DashboardViewModel(
        INetworkTrafficCollector collector,
        INetworkAdapterProvider adapterProvider,
        ILocalizationService localization,
        DispatcherQueue dispatcherQueue,
        ITrafficHistoryService? historyService = null,
        IProcessTrafficCollector? processCollector = null)
    {
        _collector = collector;
        _adapterProvider = adapterProvider;
        _localization = localization;
        _dispatcherQueue = dispatcherQueue;
        _historyService = historyService;
        _processCollector = processCollector;

        _kindKeys = new Dictionary<NetworkAdapterKind, string>
        {
            [NetworkAdapterKind.Ethernet] = "KindEthernet",
            [NetworkAdapterKind.Wireless] = "KindWireless",
            [NetworkAdapterKind.Tunnel] = "KindTunnel",
            [NetworkAdapterKind.Virtual] = "KindVirtual",
            [NetworkAdapterKind.Unknown] = "KindUnknown"
        };

        _localization.CultureChanged += OnCultureChanged;
        _collector.SpeedSampleReady += OnSpeedSample;
        _collector.NetworkChanged += OnAdaptersChanged;
        _adapterProvider.AdaptersChanged += OnAdaptersChanged;

        if (_historyService is not null)
        {
            _historyService.HistoryChanged += OnHistoryChanged;
        }

        if (_processCollector is not null)
        {
            _processCollector.SamplesReady += OnProcessSamplesReady;
            _processCollector.StatusChanged += OnProcessStatusChanged;
        }

        RefreshLocalizedStrings();
        ReloadAdapters();
        RefreshAll();
        RefreshGraph();
        RefreshToday();
        RefreshTopApp();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetActive(bool active)
    {
        _isActive = active;
        if (!active)
        {
            return;
        }

        RunOnUi(() =>
        {
            ReloadAdapters();
            RefreshAll(force: true);
            RefreshGraph();
            RefreshToday();
            RefreshTopApp();
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _isActive = false;

        _localization.CultureChanged -= OnCultureChanged;
        _collector.SpeedSampleReady -= OnSpeedSample;
        _collector.NetworkChanged -= OnAdaptersChanged;
        _adapterProvider.AdaptersChanged -= OnAdaptersChanged;

        if (_historyService is not null)
        {
            _historyService.HistoryChanged -= OnHistoryChanged;
        }

        if (_processCollector is not null)
        {
            _processCollector.SamplesReady -= OnProcessSamplesReady;
            _processCollector.StatusChanged -= OnProcessStatusChanged;
        }
    }

    public string DashboardLabel
    {
        get => _dashboardLabel;
        private set => SetProperty(ref _dashboardLabel, value);
    }

    public string DownloadLabel
    {
        get => _downloadLabel;
        private set => SetProperty(ref _downloadLabel, value);
    }

    public string UploadLabel
    {
        get => _uploadLabel;
        private set => SetProperty(ref _uploadLabel, value);
    }

    public string TotalLabel
    {
        get => _totalLabel;
        private set => SetProperty(ref _totalLabel, value);
    }

    public string ActiveAdapterLabel
    {
        get => _activeAdapterLabel;
        private set => SetProperty(ref _activeAdapterLabel, value);
    }

    public string StatusLabel
    {
        get => _statusLabel;
        private set => SetProperty(ref _statusLabel, value);
    }

    public string ConnectedLabel
    {
        get => _connectedLabel;
        private set => SetProperty(ref _connectedLabel, value);
    }

    public string DisconnectedLabel
    {
        get => _disconnectedLabel;
        private set => SetProperty(ref _disconnectedLabel, value);
    }

    public string NoActiveConnectionText
    {
        get => _noActiveConnection;
        private set => SetProperty(ref _noActiveConnection, value);
    }

    public bool HasConnection
    {
        get => _hasConnection;
        private set => SetProperty(ref _hasConnection, value);
    }

    public string DownloadText
    {
        get => _downloadText;
        private set => SetProperty(ref _downloadText, value);
    }

    public string UploadText
    {
        get => _uploadText;
        private set => SetProperty(ref _uploadText, value);
    }

    public string TotalText
    {
        get => _totalText;
        private set => SetProperty(ref _totalText, value);
    }

    public string DownloadMbpsText
    {
        get => _downloadMbpsText;
        private set => SetProperty(ref _downloadMbpsText, value);
    }

    public string UploadMbpsText
    {
        get => _uploadMbpsText;
        private set => SetProperty(ref _uploadMbpsText, value);
    }

    public string TotalMbpsText
    {
        get => _totalMbpsText;
        private set => SetProperty(ref _totalMbpsText, value);
    }

    public string ActiveAdapterName
    {
        get => _activeAdapterName;
        private set => SetProperty(ref _activeAdapterName, value);
    }

    public string ActiveAdapterKindText
    {
        get => _activeAdapterKindText;
        private set => SetProperty(ref _activeAdapterKindText, value);
    }

    public string ActiveAdapterStatusText
    {
        get => _activeAdapterStatusText;
        private set => SetProperty(ref _activeAdapterStatusText, value);
    }

    public IReadOnlyList<TrafficGraphPoint> GraphPoints
    {
        get => _graphPoints;
        private set => SetProperty(ref _graphPoints, value);
    }

    public long GraphScaleMax
    {
        get => _graphScaleMax;
        private set => SetProperty(ref _graphScaleMax, value);
    }

    public DateTime GraphReferenceTime
    {
        get => _graphReferenceTime;
        private set => SetProperty(ref _graphReferenceTime, value);
    }

    public GraphTimeRange SelectedGraphRange
    {
        get => _selectedGraphRange;
        private set
        {
            if (value == _selectedGraphRange)
            {
                return;
            }

            _selectedGraphRange = value;
            OnPropertyChanged();
            UpdateRangeSelectionFlags();
            RefreshGraph();
        }
    }

    public bool Is30SecondsSelected
    {
        get => _is30SecondsSelected;
        private set => SetProperty(ref _is30SecondsSelected, value);
    }

    public bool Is1MinuteSelected
    {
        get => _is1MinuteSelected;
        private set => SetProperty(ref _is1MinuteSelected, value);
    }

    public bool Is5MinutesSelected
    {
        get => _is5MinutesSelected;
        private set => SetProperty(ref _is5MinutesSelected, value);
    }

    public string GraphLiveTrafficLabel
    {
        get => _graphLiveTrafficLabel;
        private set => SetProperty(ref _graphLiveTrafficLabel, value);
    }

    public string GraphLast30SecondsLabel
    {
        get => _graphLast30SecondsLabel;
        private set => SetProperty(ref _graphLast30SecondsLabel, value);
    }

    public string GraphLast1MinuteLabel
    {
        get => _graphLast1MinuteLabel;
        private set => SetProperty(ref _graphLast1MinuteLabel, value);
    }

    public string GraphLast5MinutesLabel
    {
        get => _graphLast5MinutesLabel;
        private set => SetProperty(ref _graphLast5MinutesLabel, value);
    }

    public string GraphNowLabel
    {
        get => _graphNowLabel;
        private set => SetProperty(ref _graphNowLabel, value);
    }

    public string GraphDownloadSeriesLabel
    {
        get => _graphDownloadSeriesLabel;
        private set => SetProperty(ref _graphDownloadSeriesLabel, value);
    }

    public string GraphUploadSeriesLabel
    {
        get => _graphUploadSeriesLabel;
        private set => SetProperty(ref _graphUploadSeriesLabel, value);
    }

    public string TunnelAggregateHint
    {
        get => _tunnelAggregateHint;
        private set => SetProperty(ref _tunnelAggregateHint, value);
    }

    public bool HasTunnelAdapter
    {
        get => _hasTunnelAdapter;
        private set => SetProperty(ref _hasTunnelAdapter, value);
    }

    public string TodayAtGlanceLabel
    {
        get => _todayAtGlanceLabel;
        private set => SetProperty(ref _todayAtGlanceLabel, value);
    }

    public string DownloadTodayLabel
    {
        get => _downloadTodayLabel;
        private set => SetProperty(ref _downloadTodayLabel, value);
    }

    public string UploadTodayLabel
    {
        get => _uploadTodayLabel;
        private set => SetProperty(ref _uploadTodayLabel, value);
    }

    public string TotalTodayLabel
    {
        get => _totalTodayLabel;
        private set => SetProperty(ref _totalTodayLabel, value);
    }

    public string TodayDownloadText
    {
        get => _todayDownloadText;
        private set => SetProperty(ref _todayDownloadText, value);
    }

    public string TodayUploadText
    {
        get => _todayUploadText;
        private set => SetProperty(ref _todayUploadText, value);
    }

    public string TodayTotalText
    {
        get => _todayTotalText;
        private set => SetProperty(ref _todayTotalText, value);
    }

    public bool HasTodayData
    {
        get => _hasTodayData;
        private set => SetProperty(ref _hasTodayData, value);
    }

    public string TopAppNowLabel
    {
        get => _topAppNowLabel;
        private set => SetProperty(ref _topAppNowLabel, value);
    }

    public string TopAppApplicationLabel
    {
        get => _topAppApplicationLabel;
        private set => SetProperty(ref _topAppApplicationLabel, value);
    }

    public string TopAppCurrentLabel
    {
        get => _topAppCurrentLabel;
        private set => SetProperty(ref _topAppCurrentLabel, value);
    }

    public string TopAppNoDataLabel
    {
        get => _topAppNoDataLabel;
        private set => SetProperty(ref _topAppNoDataLabel, value);
    }

    public string TopAppPermissionDeniedLabel
    {
        get => _topAppPermissionDeniedLabel;
        private set => SetProperty(ref _topAppPermissionDeniedLabel, value);
    }

    public string TopAppUnavailableLabel
    {
        get => _topAppUnavailableLabel;
        private set => SetProperty(ref _topAppUnavailableLabel, value);
    }

    public string TopAppDownloadLabel
    {
        get => _topAppDownloadLabel;
        private set => SetProperty(ref _topAppDownloadLabel, value);
    }

    public string TopAppUploadLabel
    {
        get => _topAppUploadLabel;
        private set => SetProperty(ref _topAppUploadLabel, value);
    }

    public string TopAppName
    {
        get => _topAppName;
        private set => SetProperty(ref _topAppName, value);
    }

    public string TopAppRateText
    {
        get => _topAppRateText;
        private set => SetProperty(ref _topAppRateText, value);
    }

    public string TopAppDownloadRateText
    {
        get => _topAppDownloadRateText;
        private set => SetProperty(ref _topAppDownloadRateText, value);
    }

    public string TopAppUploadRateText
    {
        get => _topAppUploadRateText;
        private set => SetProperty(ref _topAppUploadRateText, value);
    }

    public string TopAppStatusText
    {
        get => _topAppStatusText;
        private set => SetProperty(ref _topAppStatusText, value);
    }

    public bool HasTopApp
    {
        get => _hasTopApp;
        private set => SetProperty(ref _hasTopApp, value);
    }

    public bool IsTopAppPermissionDenied
    {
        get => _isTopAppPermissionDenied;
        private set => SetProperty(ref _isTopAppPermissionDenied, value);
    }

    public bool IsTopAppUnavailable
    {
        get => _isTopAppUnavailable;
        private set => SetProperty(ref _isTopAppUnavailable, value);
    }

    public bool IsTopAppIdle
    {
        get => _isTopAppIdle;
        private set => SetProperty(ref _isTopAppIdle, value);
    }

    public void SelectGraphRange(GraphTimeRange range) => SelectedGraphRange = range;

    private void OnSpeedSample(object? sender, NetworkSpeedSample sample)
    {
        if (!_isActive)
        {
            return;
        }

        CoalesceRefresh();
    }

    private void CoalesceRefresh()
    {
        if (!_isActive || _refreshPending || _disposed)
        {
            return;
        }

        _refreshPending = true;

        if (_dispatcherQueue.HasThreadAccess)
        {
            _refreshPending = false;
            RefreshRates();
            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            _refreshPending = false;
            if (_isActive && !_disposed)
            {
                RefreshRates();
            }
        });
    }

    private void OnAdaptersChanged(object? sender, EventArgs e) =>
        RunOnUi(() =>
        {
            if (!_isActive)
            {
                return;
            }

            ReloadAdapters();
            RefreshAll();
        });

    private void OnCultureChanged(object? sender, EventArgs e) =>
        RunOnUi(() =>
        {
            if (!_isActive)
            {
                return;
            }

            RefreshLocalizedStrings();
            ReloadAdapters();
            RefreshAll(force: true);
            RefreshGraph();
            RefreshToday();
        });

    private void RunOnUi(Action action)
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (!_disposed)
            {
                action();
            }
        });
    }

    private void RefreshLocalizedStrings()
    {
        DashboardLabel = _localization["DashboardLabel"];
        DownloadLabel = _localization["DownloadLabel"];
        UploadLabel = _localization["UploadLabel"];
        TotalLabel = _localization["TotalRateLabel"];
        ActiveAdapterLabel = _localization["ActiveAdapterLabel"];
        StatusLabel = _localization["StatusLabel"];
        ConnectedLabel = _localization["ConnectedLabel"];
        DisconnectedLabel = _localization["DisconnectedLabel"];
        NoActiveConnectionText = _localization["NoActiveConnection"];
        GraphLiveTrafficLabel = _localization["GraphLiveTrafficLabel"];
        GraphLast30SecondsLabel = _localization["GraphLast30SecondsLabel"];
        GraphLast1MinuteLabel = _localization["GraphLast1MinuteLabel"];
        GraphLast5MinutesLabel = _localization["GraphLast5MinutesLabel"];
        GraphNowLabel = _localization["GraphNowLabel"];
        GraphDownloadSeriesLabel = _localization["GraphDownloadSeriesLabel"];
        GraphUploadSeriesLabel = _localization["GraphUploadSeriesLabel"];
        TunnelAggregateHint = _localization["TunnelAggregateHintText"];

        TodayAtGlanceLabel = _localization["TodayAtGlanceLabel"];
        DownloadTodayLabel = _localization["DownloadTodayLabel"];
        UploadTodayLabel = _localization["UploadTodayLabel"];
        TotalTodayLabel = _localization["TotalTodayLabel"];

        TopAppNowLabel = _localization["TopAppNowLabel"];
        TopAppApplicationLabel = _localization["TopAppApplicationLabel"];
        TopAppCurrentLabel = _localization["TopAppCurrentLabel"];
        TopAppNoDataLabel = _localization["TopAppNoDataLabel"];
        TopAppPermissionDeniedLabel = _localization["TopAppPermissionDeniedLabel"];
        TopAppUnavailableLabel = _localization["TopAppUnavailableLabel"];
        TopAppDownloadLabel = _localization["TopAppDownloadLabel"];
        TopAppUploadLabel = _localization["TopAppUploadLabel"];
    }

    private void ReloadAdapters()
    {
        _adapters = _adapterProvider.GetAdapters();
        UpdateHasTunnelAdapter();
    }

    private void UpdateHasTunnelAdapter() =>
        HasTunnelAdapter = _adapters.Any(a => a.Kind == NetworkAdapterKind.Tunnel && a.IsUp);

    private void RefreshRates()
    {
        _samples = _collector.GetCurrentSamples();
        RefreshAll();
    }

    private void RefreshAll(bool force = false)
    {
        _aggregate = NetworkTrafficAggregator.AggregateRates(_samples, _adapters);

        if (_aggregate is not null &&
            _graphBuffer.Add(
                _aggregate.Timestamp,
                _aggregate.DownloadBytesPerSecond,
                _aggregate.UploadBytesPerSecond))
        {
            RefreshGraph();
        }

        var download = _aggregate?.DownloadBytesPerSecond ?? 0;
        var upload = _aggregate?.UploadBytesPerSecond ?? 0;
        var total = download + upload;

        var downloadChanged = force || download != _downloadBytesPerSecond;
        var uploadChanged = force || upload != _uploadBytesPerSecond;

        _downloadBytesPerSecond = download;
        _uploadBytesPerSecond = upload;
        _totalBytesPerSecond = total;
        HasConnection = _adapters.Any(a => a.IsUp);

        if (downloadChanged)
        {
            DownloadText = DataRateFormatter.FormatAdaptive(download);
            DownloadMbpsText = DataRateFormatter.FormatMbps(download);
            OnPropertyChanged(nameof(DownloadBytesPerSecond));
        }

        if (uploadChanged)
        {
            UploadText = DataRateFormatter.FormatAdaptive(upload);
            UploadMbpsText = DataRateFormatter.FormatMbps(upload);
            OnPropertyChanged(nameof(UploadBytesPerSecond));
        }

        if (downloadChanged || uploadChanged)
        {
            TotalText = DataRateFormatter.FormatAdaptive(total);
            TotalMbpsText = DataRateFormatter.FormatMbps(total);
            OnPropertyChanged(nameof(TotalBytesPerSecond));
        }

        var preferred = _adapterProvider.GetDefaultAdapter();
        if (preferred is null)
        {
            ActiveAdapterName = NoActiveConnectionText;
            ActiveAdapterKindText = string.Empty;
            ActiveAdapterStatusText = StatusText(null);
            return;
        }

        ActiveAdapterName = preferred.Name;
        ActiveAdapterKindText = KindText(preferred.Kind);
        ActiveAdapterStatusText = StatusText(preferred.IsUp);
    }

    public long DownloadBytesPerSecond
    {
        get => _downloadBytesPerSecond;
        private set => SetProperty(ref _downloadBytesPerSecond, value);
    }

    public long UploadBytesPerSecond
    {
        get => _uploadBytesPerSecond;
        private set => SetProperty(ref _uploadBytesPerSecond, value);
    }

    public long TotalBytesPerSecond
    {
        get => _totalBytesPerSecond;
        private set => SetProperty(ref _totalBytesPerSecond, value);
    }

    private string KindText(NetworkAdapterKind kind) => _localization[_kindKeys[kind]];

    private string StatusText(bool? isUp) =>
        _localization[isUp == true ? "ConnectedLabel" : "DisconnectedLabel"];

    private void RefreshGraph()
    {
        var now = _graphBuffer.LatestTimestamp ?? DateTime.UtcNow;
        GraphPoints = _graphBuffer.Slice(_selectedGraphRange.ToDuration(), now);
        GraphScaleMax = _graphScale.Update(GraphPoints);
        GraphReferenceTime = now;
    }

    private void UpdateRangeSelectionFlags()
    {
        Is30SecondsSelected = _selectedGraphRange == GraphTimeRange.ThirtySeconds;
        Is1MinuteSelected = _selectedGraphRange == GraphTimeRange.OneMinute;
        Is5MinutesSelected = _selectedGraphRange == GraphTimeRange.FiveMinutes;
    }

    private void OnHistoryChanged(object? sender, EventArgs e) =>
        RunOnUi(() =>
        {
            if (!_isActive)
            {
                return;
            }

            RefreshToday();
        });

    private void OnProcessSamplesReady(object? sender, IReadOnlyList<ProcessTrafficSample> samples) =>
        RunOnUi(() =>
        {
            if (!_isActive)
            {
                return;
            }

            RefreshTopApp(samples);
        });

    private void OnProcessStatusChanged(object? sender, EventArgs e) =>
        RunOnUi(() =>
        {
            if (!_isActive)
            {
                return;
            }

            RefreshTopAppStatus();
            if (!IsTopAppPermissionDenied && !IsTopAppUnavailable)
            {
                RefreshTopApp();
            }
        });

    private void RefreshToday()
    {
        if (_historyService is null || !_historyService.IsAvailable)
        {
            HasTodayData = false;
            TodayDownloadText = DataSizeFormatter.Format(0);
            TodayUploadText = DataSizeFormatter.Format(0);
            TodayTotalText = DataSizeFormatter.Format(0);
            return;
        }

        var snapshot = _historyService.GetSnapshot();
        if (!snapshot.IsAvailable)
        {
            HasTodayData = false;
            TodayDownloadText = DataSizeFormatter.Format(0);
            TodayUploadText = DataSizeFormatter.Format(0);
            TodayTotalText = DataSizeFormatter.Format(0);
            return;
        }

        var today = snapshot.Today;
        HasTodayData = true;
        TodayDownloadText = DataSizeFormatter.Format(today.DownloadBytes);
        TodayUploadText = DataSizeFormatter.Format(today.UploadBytes);
        TodayTotalText = DataSizeFormatter.Format(today.TotalBytes);
    }

    private void RefreshTopApp(IReadOnlyList<ProcessTrafficSample>? samples = null)
    {
        if (_processCollector is null)
        {
            RefreshTopAppStatus();
            return;
        }

        samples ??= _processCollector.GetCurrentSamples();
        RefreshTopAppStatus();

        if (IsTopAppPermissionDenied || IsTopAppUnavailable)
        {
            HasTopApp = false;
            TopAppName = string.Empty;
            TopAppRateText = string.Empty;
            TopAppDownloadRateText = string.Empty;
            TopAppUploadRateText = string.Empty;
            return;
        }

        var consumer = ProcessSampleSelection.TopConsumer(samples);
        var hasActive = ProcessSampleSelection.HasActiveTraffic(samples);

        if (!hasActive || consumer is null)
        {
            HasTopApp = false;
            TopAppName = string.Empty;
            TopAppRateText = string.Empty;
            TopAppDownloadRateText = string.Empty;
            TopAppUploadRateText = string.Empty;
            TopAppStatusText = TopAppNoDataLabel;
            IsTopAppIdle = true;
            return;
        }

        HasTopApp = true;
        IsTopAppIdle = false;
        TopAppName = consumer.ProcessName;
        TopAppRateText = DataRateFormatter.FormatAdaptive((long)Math.Round(consumer.TotalBytesPerSecond));
        TopAppDownloadRateText = DataRateFormatter.FormatAdaptive((long)Math.Round(consumer.DownloadBytesPerSecond));
        TopAppUploadRateText = DataRateFormatter.FormatAdaptive((long)Math.Round(consumer.UploadBytesPerSecond));
        TopAppStatusText = string.Empty;
    }

    private void RefreshTopAppStatus()
    {
        if (_processCollector is null)
        {
            IsTopAppPermissionDenied = false;
            IsTopAppUnavailable = false;
            TopAppStatusText = string.Empty;
            return;
        }

        switch (_processCollector.Status)
        {
            case ProcessTrafficCollectorStatus.PermissionDenied:
                IsTopAppPermissionDenied = true;
                IsTopAppUnavailable = false;
                TopAppStatusText = TopAppPermissionDeniedLabel;
                break;
            case ProcessTrafficCollectorStatus.Failed:
            case ProcessTrafficCollectorStatus.Stopped:
                IsTopAppPermissionDenied = false;
                IsTopAppUnavailable = true;
                TopAppStatusText = TopAppUnavailableLabel;
                break;
            default:
                IsTopAppPermissionDenied = false;
                IsTopAppUnavailable = false;
                if (!HasTopApp)
                {
                    TopAppStatusText = TopAppNoDataLabel;
                }

                break;
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
