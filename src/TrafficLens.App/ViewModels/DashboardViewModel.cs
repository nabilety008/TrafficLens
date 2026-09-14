using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Conversion;
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

    private IReadOnlyList<NetworkAdapterInfo> _adapters = Array.Empty<NetworkAdapterInfo>();
    private IReadOnlyList<NetworkSpeedSample> _samples = Array.Empty<NetworkSpeedSample>();
    private NetworkSpeedSample? _aggregate;

    private long _downloadBytesPerSecond;
    private long _uploadBytesPerSecond;
    private long _totalBytesPerSecond;
    private bool _hasConnection;

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

        RefreshLocalizedStrings();
        ReloadAdapters();
        RefreshAll();
    }

    public ObservableCollection<AdapterListItemViewModel> Adapters { get; } = new();

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

    private void OnSpeedSample(object? sender, NetworkSpeedSample sample) => RunOnUi(RefreshRates);

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
            RefreshAll();
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

    private void RefreshAll()
    {
        _aggregate = NetworkTrafficAggregator.AggregateRates(_samples, _adapters);

        DownloadBytesPerSecond = _aggregate?.DownloadBytesPerSecond ?? 0;
        UploadBytesPerSecond = _aggregate?.UploadBytesPerSecond ?? 0;
        TotalBytesPerSecond = DownloadBytesPerSecond + UploadBytesPerSecond;
        HasConnection = _adapters.Any(a => a.IsUp);

        DownloadText = DataRateFormatter.FormatAdaptive(DownloadBytesPerSecond);
        UploadText = DataRateFormatter.FormatAdaptive(UploadBytesPerSecond);
        TotalText = DataRateFormatter.FormatAdaptive(TotalBytesPerSecond);
        DownloadMbpsText = DataRateFormatter.FormatMbps(DownloadBytesPerSecond);
        UploadMbpsText = DataRateFormatter.FormatMbps(UploadBytesPerSecond);
        TotalMbpsText = DataRateFormatter.FormatMbps(TotalBytesPerSecond);

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
}