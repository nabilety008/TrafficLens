using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.Localization;
using TrafficLens.Core.Models;
using TrafficLens.Core.Selection;
using TrafficLens.Network.Process;
using TrafficLens.WinUI.Infrastructure;

namespace TrafficLens.WinUI.ViewModels;

public sealed class ApplicationsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IProcessTrafficCollector _collector;
    private readonly ILocalizationService _localization;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ProcessIconCache _iconCache;
    private readonly Dictionary<ProcessInstanceId, ProcessRowViewModel> _rows = new();
    private readonly List<ProcessInstanceId> _displayedIdentities = new();

    private IReadOnlyList<ProcessTrafficSample> _samples = Array.Empty<ProcessTrafficSample>();
    private CultureInfo _culture;

    private string _searchText = string.Empty;
    private ProcessSortKey _sortKey = ProcessSortKey.TotalRate;

    private ProcessTrafficCollectorStatus _collectorStatus;
    private string _statusText = string.Empty;
    private string _statusDetailText = string.Empty;
    private bool _isStatusBannerVisible;
    private bool _isPermissionDenied;
    private bool _isStartMonitoringVisible;
    private bool _hasStatusDetail;

    private bool _hasActiveTraffic;
    private bool _isIdle;
    private string _topConsumerName = string.Empty;
    private string _topConsumerDownloadRateText = string.Empty;
    private string _topConsumerUploadRateText = string.Empty;
    private string _topConsumerTotalRateText = string.Empty;
    private string _topDownloadName = string.Empty;
    private string _topDownloadRateText = string.Empty;
    private string _topUploadName = string.Empty;
    private string _topUploadRateText = string.Empty;
    private ImageSource? _topConsumerIcon;
    private ImageSource? _topDownloadIcon;
    private ImageSource? _topUploadIcon;

    private string _applicationsLabel = string.Empty;
    private string _topConsumerNowLabel = string.Empty;
    private string _topDownloadLabel = string.Empty;
    private string _topUploadLabel = string.Empty;
    private string _totalLabel = string.Empty;
    private string _pidLabel = string.Empty;
    private string _totalTransferredLabel = string.Empty;
    private string _searchPlaceholder = string.Empty;
    private string _sortByLabel = string.Empty;
    private string _noActiveTrafficText = string.Empty;
    private string _processStateLabel = string.Empty;
    private string _statusStartingText = string.Empty;
    private string _statusStoppedText = string.Empty;
    private string _statusFailedText = string.Empty;
    private string _adminPermissionRequiredText = string.Empty;
    private string _permissionDeniedDetailText = string.Empty;
    private string _monitoringFailedDetailText = string.Empty;
    private string _runningText = string.Empty;
    private string _exitedText = string.Empty;
    private string _restartAsAdministratorLabel = string.Empty;
    private string _startMonitoringLabel = string.Empty;
    private string _currentSpeedLabel = string.Empty;
    private string _processLabel = string.Empty;

    private bool _refreshPending;
    private bool _isActive = true;
    private bool _disposed;

    public ApplicationsViewModel(
        IProcessTrafficCollector collector,
        ILocalizationService localization,
        DispatcherQueue dispatcherQueue,
        ProcessIconCache iconCache)
    {
        _collector = collector;
        _localization = localization;
        _dispatcherQueue = dispatcherQueue;
        _iconCache = iconCache;
        _culture = localization.CurrentCulture;

        _collector.StatusChanged += OnStatusChanged;
        _collector.SamplesReady += OnSamplesReady;
        _localization.CultureChanged += OnCultureChanged;
        _iconCache.IconReady += OnIconReady;

        RefreshLocalizedStrings();
        UpdateStatus();
        RefreshSamples(_collector.GetCurrentSamples());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProcessRowViewModel> Processes { get; } = new();

    public ObservableCollection<ApplicationSortOption> SortOptions { get; } = new();

    public void SetActive(bool active)
    {
        _isActive = active;
        if (!active)
        {
            return;
        }

        RunOnUi(() =>
        {
            RefreshSamples(_collector.GetCurrentSamples());
            UpdateStatus();
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

        _collector.StatusChanged -= OnStatusChanged;
        _collector.SamplesReady -= OnSamplesReady;
        _localization.CultureChanged -= OnCultureChanged;
        _iconCache.IconReady -= OnIconReady;
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RebuildDisplayList(force: true);
            }
        }
    }

    public ProcessSortKey SortKey
    {
        get => _sortKey;
        set
        {
            if (SetProperty(ref _sortKey, value))
            {
                RebuildDisplayList(force: true);
            }
        }
    }

    public bool HasActiveTraffic
    {
        get => _hasActiveTraffic;
        private set => SetProperty(ref _hasActiveTraffic, value);
    }

    public bool IsIdle
    {
        get => _isIdle;
        private set => SetProperty(ref _isIdle, value);
    }

    public string TopConsumerName
    {
        get => _topConsumerName;
        private set => SetProperty(ref _topConsumerName, value);
    }

    public string TopConsumerDownloadRateText
    {
        get => _topConsumerDownloadRateText;
        private set => SetProperty(ref _topConsumerDownloadRateText, value);
    }

    public string TopConsumerUploadRateText
    {
        get => _topConsumerUploadRateText;
        private set => SetProperty(ref _topConsumerUploadRateText, value);
    }

    public string TopConsumerTotalRateText
    {
        get => _topConsumerTotalRateText;
        private set => SetProperty(ref _topConsumerTotalRateText, value);
    }

    public string TopDownloadName
    {
        get => _topDownloadName;
        private set => SetProperty(ref _topDownloadName, value);
    }

    public string TopDownloadRateText
    {
        get => _topDownloadRateText;
        private set => SetProperty(ref _topDownloadRateText, value);
    }

    public string TopUploadName
    {
        get => _topUploadName;
        private set => SetProperty(ref _topUploadName, value);
    }

    public string TopUploadRateText
    {
        get => _topUploadRateText;
        private set => SetProperty(ref _topUploadRateText, value);
    }

    public ImageSource? TopConsumerIcon
    {
        get => _topConsumerIcon;
        private set => SetProperty(ref _topConsumerIcon, value);
    }

    public ImageSource? TopDownloadIcon
    {
        get => _topDownloadIcon;
        private set => SetProperty(ref _topDownloadIcon, value);
    }

    public ImageSource? TopUploadIcon
    {
        get => _topUploadIcon;
        private set => SetProperty(ref _topUploadIcon, value);
    }

    public ProcessTrafficCollectorStatus CollectorStatus => _collectorStatus;

    public bool IsStatusBannerVisible
    {
        get => _isStatusBannerVisible;
        private set => SetProperty(ref _isStatusBannerVisible, value);
    }

    public bool IsPermissionDenied
    {
        get => _isPermissionDenied;
        private set => SetProperty(ref _isPermissionDenied, value);
    }

    public bool IsStartMonitoringVisible
    {
        get => _isStartMonitoringVisible;
        private set => SetProperty(ref _isStartMonitoringVisible, value);
    }

    public bool HasStatusDetail
    {
        get => _hasStatusDetail;
        private set => SetProperty(ref _hasStatusDetail, value);
    }

    public string RestartAsAdministratorLabel
    {
        get => _restartAsAdministratorLabel;
        private set => SetProperty(ref _restartAsAdministratorLabel, value);
    }

    public string StartMonitoringLabel
    {
        get => _startMonitoringLabel;
        private set => SetProperty(ref _startMonitoringLabel, value);
    }

    public string CurrentSpeedLabel
    {
        get => _currentSpeedLabel;
        private set => SetProperty(ref _currentSpeedLabel, value);
    }

    public string ProcessLabel
    {
        get => _processLabel;
        private set => SetProperty(ref _processLabel, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string StatusDetailText
    {
        get => _statusDetailText;
        private set => SetProperty(ref _statusDetailText, value);
    }

    public string ApplicationsLabel
    {
        get => _applicationsLabel;
        private set => SetProperty(ref _applicationsLabel, value);
    }

    public string TopConsumerNowLabel
    {
        get => _topConsumerNowLabel;
        private set => SetProperty(ref _topConsumerNowLabel, value);
    }

    public string TopDownloadLabel
    {
        get => _topDownloadLabel;
        private set => SetProperty(ref _topDownloadLabel, value);
    }

    public string TopUploadLabel
    {
        get => _topUploadLabel;
        private set => SetProperty(ref _topUploadLabel, value);
    }

    public string TotalLabel
    {
        get => _totalLabel;
        private set => SetProperty(ref _totalLabel, value);
    }

    public string PidLabel
    {
        get => _pidLabel;
        private set => SetProperty(ref _pidLabel, value);
    }

    public string TotalTransferredLabel
    {
        get => _totalTransferredLabel;
        private set => SetProperty(ref _totalTransferredLabel, value);
    }

    public string SearchPlaceholder
    {
        get => _searchPlaceholder;
        private set => SetProperty(ref _searchPlaceholder, value);
    }

    public string SortByLabel
    {
        get => _sortByLabel;
        private set => SetProperty(ref _sortByLabel, value);
    }

    public string NoActiveTrafficText
    {
        get => _noActiveTrafficText;
        private set => SetProperty(ref _noActiveTrafficText, value);
    }

    public string ProcessStateLabel
    {
        get => _processStateLabel;
        private set => SetProperty(ref _processStateLabel, value);
    }

    public void StartMonitoring()
    {
        if (_collector.Status is ProcessTrafficCollectorStatus.Starting or ProcessTrafficCollectorStatus.Running)
        {
            return;
        }

        _ = _collector.StartAsync(CancellationToken.None);
    }

    public void RestartAsAdministrator()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo(path)
            {
                UseShellExecute = true,
                Verb = "runas"
            };
            System.Diagnostics.Process.Start(startInfo);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        Environment.Exit(0);
    }

    private void OnSamplesReady(object? sender, IReadOnlyList<ProcessTrafficSample> samples)
    {
        if (!_isActive || _disposed)
        {
            return;
        }

        CoalesceRefresh(() => RefreshSamples(samples));
    }

    private void OnStatusChanged(object? sender, EventArgs e)
    {
        if (!_isActive || _disposed)
        {
            return;
        }

        RunOnUi(UpdateStatus);
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        if (!_isActive || _disposed)
        {
            return;
        }

        RunOnUi(() =>
        {
            RefreshLocalizedStrings();
            RefreshSamples(_samples);
            UpdateStatus();
        });
    }

    private void CoalesceRefresh(Action action)
    {
        if (_refreshPending || _disposed)
        {
            return;
        }

        _refreshPending = true;

        if (_dispatcherQueue.HasThreadAccess)
        {
            _refreshPending = false;
            if (_isActive && !_disposed)
            {
                action();
            }

            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            _refreshPending = false;
            if (_isActive && !_disposed)
            {
                action();
            }
        });
    }

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
        _culture = _localization.CurrentCulture;

        ApplicationsLabel = _localization["ApplicationsLabel"];
        TopConsumerNowLabel = _localization["TopConsumerNowLabel"];
        TopDownloadLabel = _localization["TopDownloadLabel"];
        TopUploadLabel = _localization["TopUploadLabel"];
        TotalLabel = _localization["TotalRateLabel"];
        PidLabel = _localization["PidLabel"];
        TotalTransferredLabel = _localization["TotalTransferredLabel"];
        SearchPlaceholder = _localization["SearchPlaceholder"];
        SortByLabel = _localization["SortByLabel"];
        NoActiveTrafficText = _localization["NoActiveTrafficLabel"];
        ProcessStateLabel = _localization["StatusLabel"];
        _statusStartingText = _localization["StatusStartingLabel"];
        _statusStoppedText = _localization["MonitoringStoppedLabel"];
        _statusFailedText = _localization["MonitoringFailedLabel"];
        _adminPermissionRequiredText = _localization["AdminPermissionRequiredLabel"];
        _permissionDeniedDetailText = _localization["PermissionDeniedDetailLabel"];
        _monitoringFailedDetailText = _localization["MonitoringFailedDetailLabel"];
        _runningText = _localization["RunningLabel"];
        _exitedText = _localization["ExitedLabel"];
        _restartAsAdministratorLabel = _localization["RestartAsAdministratorLabel"];
        _startMonitoringLabel = _localization["StartMonitoringLabel"];
        CurrentSpeedLabel = _localization["CurrentSpeedLabel"];
        ProcessLabel = _localization["ProcessLabel"];
        RestartAsAdministratorLabel = _restartAsAdministratorLabel;
        StartMonitoringLabel = _startMonitoringLabel;

        SortOptions.Clear();
        SortOptions.Add(new ApplicationSortOption(ProcessSortKey.TotalRate, _localization["SortTotalRateLabel"]));
        SortOptions.Add(new ApplicationSortOption(ProcessSortKey.DownloadRate, _localization["SortDownloadRateLabel"]));
        SortOptions.Add(new ApplicationSortOption(ProcessSortKey.UploadRate, _localization["SortUploadRateLabel"]));
        SortOptions.Add(new ApplicationSortOption(ProcessSortKey.TotalTransferred, _localization["TotalTransferredLabel"]));
        SortOptions.Add(new ApplicationSortOption(ProcessSortKey.Downloaded, _localization["SortDownloadedLabel"]));
        SortOptions.Add(new ApplicationSortOption(ProcessSortKey.Uploaded, _localization["SortUploadedLabel"]));
        SortOptions.Add(new ApplicationSortOption(ProcessSortKey.Name, _localization["SortNameLabel"]));
    }

    private void RefreshSamples(IReadOnlyList<ProcessTrafficSample> samples)
    {
        _samples = samples;
        _iconCache.BeginRefresh();

        var seen = new HashSet<ProcessInstanceId>(samples.Count);
        foreach (var sample in samples)
        {
            var identity = new ProcessInstanceId(sample.ProcessId, sample.ProcessStartTimeUtcTicks);
            seen.Add(identity);

            if (!_rows.TryGetValue(identity, out var row))
            {
                row = new ProcessRowViewModel(identity);
                _rows[identity] = row;
            }

            row.Update(sample, _culture, _runningText, _exitedText, _iconCache);
        }

        if (seen.Count != _rows.Count)
        {
            foreach (var identity in _rows.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                _rows.Remove(identity);
            }
        }

        RefreshTopConsumers();
        RebuildDisplayList(force: false);
    }

    private void RefreshTopConsumers()
    {
        HasActiveTraffic = ProcessSampleSelection.HasActiveTraffic(_samples);
        RefreshIdleState();

        var consumer = ProcessSampleSelection.TopConsumer(_samples);
        if (!HasActiveTraffic || consumer is null)
        {
            TopConsumerName = string.Empty;
            TopConsumerDownloadRateText = string.Empty;
            TopConsumerUploadRateText = string.Empty;
            TopConsumerTotalRateText = string.Empty;
            TopConsumerIcon = null;
            ClearSideCards();
            return;
        }

        TopConsumerName = consumer.ProcessName;
        TopConsumerDownloadRateText = DataRateFormatter.FormatAdaptive((long)Math.Round(consumer.DownloadBytesPerSecond), _culture);
        TopConsumerUploadRateText = DataRateFormatter.FormatAdaptive((long)Math.Round(consumer.UploadBytesPerSecond), _culture);
        TopConsumerTotalRateText = DataRateFormatter.FormatAdaptive((long)Math.Round(consumer.TotalBytesPerSecond), _culture);
        TopConsumerIcon = ResolveCardIcon(consumer);

        var download = ProcessSampleSelection.TopDownload(_samples);
        var upload = ProcessSampleSelection.TopUpload(_samples);
        TopDownloadName = download?.ProcessName ?? string.Empty;
        TopDownloadRateText = download is null
            ? string.Empty
            : DataRateFormatter.FormatAdaptive((long)Math.Round(download.DownloadBytesPerSecond), _culture);
        TopUploadName = upload?.ProcessName ?? string.Empty;
        TopUploadRateText = upload is null
            ? string.Empty
            : DataRateFormatter.FormatAdaptive((long)Math.Round(upload.UploadBytesPerSecond), _culture);
        TopDownloadIcon = ResolveCardIcon(download);
        TopUploadIcon = ResolveCardIcon(upload);
    }

    private void ClearSideCards()
    {
        TopDownloadName = string.Empty;
        TopDownloadRateText = string.Empty;
        TopUploadName = string.Empty;
        TopUploadRateText = string.Empty;
        TopDownloadIcon = null;
        TopUploadIcon = null;
    }

    private ImageSource? ResolveCardIcon(ProcessTrafficSample? sample)
    {
        if (sample is null)
        {
            return null;
        }

        var path = sample.ExecutablePath;
        var available = sample.IconAvailable && !string.IsNullOrWhiteSpace(path);
        var image = _iconCache.TryGet(path, available);
        if (image is null)
        {
            _iconCache.Request(path, available);
        }

        return image;
    }

    private void OnIconReady(object? sender, string path)
    {
        if (!_isActive || _disposed)
        {
            return;
        }

        RunOnUi(() =>
        {
            foreach (var row in _rows.Values)
            {
                row.OnIconReady(_iconCache);
            }

            TopConsumerIcon = ResolveCardIcon(ProcessSampleSelection.TopConsumer(_samples));
            TopDownloadIcon = ResolveCardIcon(ProcessSampleSelection.TopDownload(_samples));
            TopUploadIcon = ResolveCardIcon(ProcessSampleSelection.TopUpload(_samples));
        });
    }

    private void UpdateStatus()
    {
        _collectorStatus = _collector.Status;
        StatusDetailText = LocalizedStatusDetail(_collectorStatus);
        HasStatusDetail = !string.IsNullOrWhiteSpace(StatusDetailText);

        switch (_collectorStatus)
        {
            case ProcessTrafficCollectorStatus.Starting:
                IsStatusBannerVisible = true;
                IsPermissionDenied = false;
                IsStartMonitoringVisible = false;
                StatusText = _statusStartingText;
                break;
            case ProcessTrafficCollectorStatus.PermissionDenied:
                IsStatusBannerVisible = true;
                IsPermissionDenied = true;
                IsStartMonitoringVisible = false;
                StatusText = _adminPermissionRequiredText;
                break;
            case ProcessTrafficCollectorStatus.Failed:
                IsStatusBannerVisible = true;
                IsPermissionDenied = false;
                IsStartMonitoringVisible = true;
                StatusText = _statusFailedText;
                break;
            case ProcessTrafficCollectorStatus.Stopped:
                IsStatusBannerVisible = true;
                IsPermissionDenied = false;
                IsStartMonitoringVisible = true;
                StatusText = _statusStoppedText;
                break;
            default:
                IsStatusBannerVisible = false;
                IsPermissionDenied = false;
                IsStartMonitoringVisible = false;
                StatusText = string.Empty;
                break;
        }

        RefreshIdleState();
        OnPropertyChanged(nameof(CollectorStatus));
    }

    private void RefreshIdleState()
    {
        var collectorUsable = _collectorStatus is ProcessTrafficCollectorStatus.Running
            or ProcessTrafficCollectorStatus.Starting;
        IsIdle = collectorUsable && !HasActiveTraffic;
    }

    private string LocalizedStatusDetail(ProcessTrafficCollectorStatus status) => status switch
    {
        ProcessTrafficCollectorStatus.PermissionDenied => _permissionDeniedDetailText,
        ProcessTrafficCollectorStatus.Failed => _monitoringFailedDetailText,
        _ => string.Empty
    };

    private void RebuildDisplayList(bool force)
    {
        IEnumerable<ProcessTrafficSample> source = _samples;
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var query = _searchText.Trim();
            source = source.Where(s =>
                s.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || s.ProcessId.ToString(_culture).Contains(query, StringComparison.Ordinal));
        }

        var ordered = source
            .OrderBy(s => s, ProcessSampleSort.Create(_sortKey))
            .ToList();

        if (!force && SameIdentities(ordered, _displayedIdentities))
        {
            return;
        }

        Processes.Clear();
        _displayedIdentities.Clear();
        foreach (var sample in ordered)
        {
            var identity = new ProcessInstanceId(sample.ProcessId, sample.ProcessStartTimeUtcTicks);
            if (_rows.TryGetValue(identity, out var row))
            {
                Processes.Add(row);
                _displayedIdentities.Add(identity);
            }
        }
    }

    private static bool SameIdentities(
        IReadOnlyList<ProcessTrafficSample> samples,
        IReadOnlyList<ProcessInstanceId> identities)
    {
        if (samples.Count != identities.Count)
        {
            return false;
        }

        for (var i = 0; i < samples.Count; i++)
        {
            if (samples[i].ProcessId != identities[i].ProcessId
                || samples[i].ProcessStartTimeUtcTicks != identities[i].StartTimeUtcTicks)
            {
                return false;
            }
        }

        return true;
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
