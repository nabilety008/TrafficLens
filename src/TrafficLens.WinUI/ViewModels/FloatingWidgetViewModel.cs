using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.Localization;
using TrafficLens.Core.Models;
using TrafficLens.Network.Aggregation;

namespace TrafficLens.WinUI.ViewModels;

public sealed class FloatingWidgetViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly INetworkTrafficCollector _collector;
    private readonly INetworkAdapterProvider _adapterProvider;
    private readonly ILocalizationService _localization;
    private readonly DispatcherQueue _dispatcherQueue;

    private IReadOnlyList<NetworkSpeedSample> _samples = Array.Empty<NetworkSpeedSample>();
    private IReadOnlyList<NetworkAdapterInfo> _adapters = Array.Empty<NetworkAdapterInfo>();

    private string _widgetTitleLabel = string.Empty;
    private string _downloadLabel = string.Empty;
    private string _uploadLabel = string.Empty;
    private string _totalLabel = string.Empty;
    private string _alwaysOnTopLabel = string.Empty;
    private string _downloadText = "0 B/s";
    private string _uploadText = "0 B/s";
    private string _totalText = "0 B/s";
    private bool _isPinned = true;
    private bool _refreshPending;
    private bool _suspended;

    public FloatingWidgetViewModel(
        INetworkTrafficCollector collector,
        INetworkAdapterProvider adapterProvider,
        ILocalizationService localization,
        DispatcherQueue dispatcherQueue)
    {
        _collector = collector;
        _adapterProvider = adapterProvider;
        _localization = localization;
        _dispatcherQueue = dispatcherQueue;

        _suspended = false;
        AttachEvents();
        RefreshLocalizedStrings();
        ReloadAdapters();
        RefreshRates();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? CloseRequested;

    public event EventHandler<bool>? PinStateChanged;

    public string WidgetTitleLabel
    {
        get => _widgetTitleLabel;
        private set => SetProperty(ref _widgetTitleLabel, value);
    }

    public string AlwaysOnTopLabel
    {
        get => _alwaysOnTopLabel;
        private set => SetProperty(ref _alwaysOnTopLabel, value);
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

    public bool IsPinned
    {
        get => _isPinned;
        internal set
        {
            if (SetProperty(ref _isPinned, value))
            {
                OnPropertyChanged(nameof(PinIcon));
            }
        }
    }

    public string PinIcon => IsPinned ? "\U0001F4CC" : "\U0001F4CD";

    public void Dispose()
    {
        if (!_suspended)
        {
            DetachEvents();
        }
    }

    public void Suspend()
    {
        if (_suspended)
        {
            return;
        }

        _suspended = true;
        DetachEvents();
    }

    public void Resume()
    {
        if (!_suspended)
        {
            return;
        }

        _suspended = false;
        AttachEvents();
        RefreshLocalizedStrings();
        RefreshRates();
    }

    public void RequestClose() => CloseRequested?.Invoke(this, EventArgs.Empty);

    public void TogglePin()
    {
        IsPinned = !IsPinned;
        PinStateChanged?.Invoke(this, IsPinned);
    }

    private void AttachEvents()
    {
        _collector.SpeedSampleReady += OnSpeedSample;
        _collector.NetworkChanged += OnAdaptersChanged;
        _adapterProvider.AdaptersChanged += OnAdaptersChanged;
        _localization.CultureChanged += OnCultureChanged;
    }

    private void DetachEvents()
    {
        _collector.SpeedSampleReady -= OnSpeedSample;
        _collector.NetworkChanged -= OnAdaptersChanged;
        _adapterProvider.AdaptersChanged -= OnAdaptersChanged;
        _localization.CultureChanged -= OnCultureChanged;
    }

    private void OnSpeedSample(object? sender, NetworkSpeedSample sample) => CoalesceRefresh();

    private void CoalesceRefresh()
    {
        if (_refreshPending)
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
            RefreshRates();
        });
    }

    private void OnAdaptersChanged(object? sender, EventArgs e) =>
        RunOnUi(() =>
        {
            ReloadAdapters();
            RefreshRates();
        });

    private void OnCultureChanged(object? sender, EventArgs e) =>
        RunOnUi(() =>
        {
            RefreshLocalizedStrings();
            RefreshRates();
        });

    private void RunOnUi(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcherQueue.TryEnqueue(() => action());
    }

    private void RefreshLocalizedStrings()
    {
        WidgetTitleLabel = _localization["FloatingWidgetLabel"];
        DownloadLabel = _localization["DownloadLabel"];
        UploadLabel = _localization["UploadLabel"];
        TotalLabel = _localization["TotalRateLabel"];
        AlwaysOnTopLabel = _localization["AlwaysOnTopLabel"];
    }

    private void ReloadAdapters() => _adapters = _adapterProvider.GetAdapters();

    private void RefreshRates()
    {
        _samples = _collector.GetCurrentSamples();
        var aggregate = NetworkTrafficAggregator.AggregateRates(_samples, _adapters);
        var download = aggregate?.DownloadBytesPerSecond ?? 0;
        var upload = aggregate?.UploadBytesPerSecond ?? 0;

        DownloadText = DataRateFormatter.FormatAdaptive(download);
        UploadText = DataRateFormatter.FormatAdaptive(upload);
        TotalText = DataRateFormatter.FormatAdaptive(download + upload);
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
