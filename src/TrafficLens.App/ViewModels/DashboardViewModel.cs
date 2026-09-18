using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.Graph;
using TrafficLens.Core.Localization;
using TrafficLens.Core.Models;
using TrafficLens.Network.Aggregation;

namespace TrafficLens.App.ViewModels;

public sealed class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly INetworkTrafficCollector _collector;
    private readonly INetworkAdapterProvider _adapterProvider;
    private readonly ILocalizationService _localization;
    private readonly Dispatcher? _dispatcher;
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
    private bool _is1MinuteSelected;
    private bool _is5MinutesSelected;
    private bool _refreshPending;

    private string _dashboardLabel = string.Empty;
    private string _downloadLabel = string.Empty;
    private string _uploadLabel = string.Empty;
    private string _totalLabel = string.Empty;
    private string _activeAdapterLabel = string.Empty;
    private string _networkAdaptersLabel = string.Empty;
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

    public DashboardViewModel(
        INetworkTrafficCollector collector,
        INetworkAdapterProvider adapterProvider,
        ILocalizationService localization)
    {
        _collector = collector;
        _adapterProvider = adapterProvider;
        _localization = localization;
        _dispatcher = Application.Current?.Dispatcher;

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

        SelectGraphRangeCommand = new Commands.RelayCommand(ExecuteSelectGraphRange);

        RefreshLocalizedStrings();
        ReloadAdapters();
        RefreshAll();
        RefreshGraph();
        UpdateRangeSelectionFlags();
    }

    public ObservableCollection<AdapterListItemViewModel> Adapters { get; } = new();

    public System.Windows.Input.ICommand SelectGraphRangeCommand { get; }

    public void Dispose()
    {
        _localization.CultureChanged -= OnCultureChanged;
        _collector.SpeedSampleReady -= OnSpeedSample;
        _collector.NetworkChanged -= OnAdaptersChanged;
        _adapterProvider.AdaptersChanged -= OnAdaptersChanged;
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

    public string NetworkAdaptersLabel
    {
        get => _networkAdaptersLabel;
        private set => SetProperty(ref _networkAdaptersLabel, value);
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

    public double GraphWindowSeconds => _selectedGraphRange.ToDuration().TotalSeconds;

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
            OnPropertyChanged(nameof(GraphWindowSeconds));
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

    private void OnSpeedSample(object? sender, NetworkSpeedSample sample) => CoalesceRefresh();

    private void CoalesceRefresh()
    {
        if (_refreshPending)
        {
            return;
        }

        _refreshPending = true;

        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            _refreshPending = false;
            RefreshRates();
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            _refreshPending = false;
            RefreshRates();
        });
    }

    private void OnAdaptersChanged(object? sender, EventArgs e) =>
        RunOnUi(() =>
        {
            ReloadAdapters();
            RefreshAll();
        });

    private void OnCultureChanged(object? sender, EventArgs e) =>
        RunOnUi(() =>
        {
            RefreshLocalizedStrings();
            ReloadAdapters();
            RefreshAll(force: true);
            RefreshGraph();
        });

    private void RunOnUi(Action action)
    {
        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.InvokeAsync(action);
    }

    private void RefreshLocalizedStrings()
    {
        DashboardLabel = _localization["DashboardLabel"];
        DownloadLabel = _localization["DownloadLabel"];
        UploadLabel = _localization["UploadLabel"];
        TotalLabel = _localization["TotalRateLabel"];
        ActiveAdapterLabel = _localization["ActiveAdapterLabel"];
        NetworkAdaptersLabel = _localization["NetworkAdaptersLabel"];
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
    }

    private void ReloadAdapters()
    {
        var newAdapters = _adapterProvider.GetAdapters();

        if (SameAdapterSet(newAdapters, _adapters))
        {
            _adapters = newAdapters;
            UpdateAdapterItemStates();
            return;
        }

        _adapters = newAdapters;

        Adapters.Clear();
        foreach (var adapter in _adapters)
        {
            Adapters.Add(new AdapterListItemViewModel(
                adapter.Id,
                adapter.Name,
                KindText(adapter.Kind),
                StatusText(adapter.IsUp)));
        }

        UpdateAdapterItemRates();
    }

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

        DownloadBytesPerSecond = download;
        UploadBytesPerSecond = upload;
        TotalBytesPerSecond = total;
        HasConnection = _adapters.Any(a => a.IsUp);

        if (downloadChanged)
        {
            DownloadText = DataRateFormatter.FormatAdaptive(download);
            DownloadMbpsText = DataRateFormatter.FormatMbps(download);
        }

        if (uploadChanged)
        {
            UploadText = DataRateFormatter.FormatAdaptive(upload);
            UploadMbpsText = DataRateFormatter.FormatMbps(upload);
        }

        if (downloadChanged || uploadChanged)
        {
            TotalText = DataRateFormatter.FormatAdaptive(total);
            TotalMbpsText = DataRateFormatter.FormatMbps(total);
        }

        UpdateAdapterItemRates();

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

    private void UpdateAdapterItemRates()
    {
        foreach (var item in Adapters)
        {
            var sample = _samples.FirstOrDefault(s => s.AdapterId.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
            if (sample is null)
            {
                var adapter = _adapters.FirstOrDefault(a => a.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
                if (adapter is null || !adapter.IsUp)
                {
                    item.UpdateRates(0, 0);
                    continue;
                }
            }

            item.UpdateRates(sample?.DownloadBytesPerSecond ?? 0, sample?.UploadBytesPerSecond ?? 0);
        }
    }

    private void UpdateAdapterItemStates()
    {
        foreach (var item in Adapters)
        {
            var adapter = _adapters.FirstOrDefault(a => a.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
            if (adapter is not null)
            {
                item.UpdateState(KindText(adapter.Kind), StatusText(adapter.IsUp));
            }
        }
    }

    private static bool SameAdapterSet(
        IReadOnlyList<NetworkAdapterInfo> left,
        IReadOnlyList<NetworkAdapterInfo> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var leftAdapter in left)
        {
            if (!right.Any(a => a.Id.Equals(leftAdapter.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return true;
    }

    private string KindText(NetworkAdapterKind kind) => _localization[_kindKeys[kind]];

    private string StatusText(bool? isUp) =>
        _localization[isUp == true ? "ConnectedLabel" : "DisconnectedLabel"];

    private void ExecuteSelectGraphRange(object? parameter)
    {
        if (parameter is string text && int.TryParse(text, out var seconds))
        {
            SelectedGraphRange = GraphTimeRangeExtensions.FromSeconds(seconds);
        }
    }

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
}