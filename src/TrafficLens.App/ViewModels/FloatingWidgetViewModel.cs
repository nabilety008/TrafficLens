using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TrafficLens.App.Commands;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.Localization;
using TrafficLens.Core.Models;
using TrafficLens.Network.Aggregation;

namespace TrafficLens.App.ViewModels;

/// <summary>
/// Compact always-on-top widget. Rides the same live rate pipeline as the
/// dashboard (ADR-009/010 aggregate semantics; no own polling or timers) and
/// only formats the current aggregate for presentation. Window show/hide and
/// position persistence are owned by <see cref="Services.FloatingWidgetService"/>.
/// </summary>
public sealed class FloatingWidgetViewModel : ViewModelBase, IDisposable
{
    private readonly INetworkTrafficCollector _collector;
    private readonly INetworkAdapterProvider _adapterProvider;
    private readonly ILocalizationService _localization;
    private readonly Dispatcher? _dispatcher;

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

    public FloatingWidgetViewModel(
        INetworkTrafficCollector collector,
        INetworkAdapterProvider adapterProvider,
        ILocalizationService localization)
    {
        _collector = collector;
        _adapterProvider = adapterProvider;
        _localization = localization;
        _dispatcher = Application.Current?.Dispatcher;

        _collector.SpeedSampleReady += OnSpeedSample;
        _collector.NetworkChanged += OnAdaptersChanged;
        _adapterProvider.AdaptersChanged += OnAdaptersChanged;
        _localization.CultureChanged += OnCultureChanged;

        TogglePinCommand = new RelayCommand(ExecuteTogglePin);
        CloseWidgetCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));

        RefreshLocalizedStrings();
        ReloadAdapters();
        RefreshRates();
    }

    public event EventHandler? CloseRequested;

    public event EventHandler<bool>? PinStateChanged;

    public ICommand TogglePinCommand { get; }

    public ICommand CloseWidgetCommand { get; }

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
        DetachEvents();
    }

    /// <summary>
    /// Temporarily detach from live data events while the widget is hidden.
    /// Lighter than Dispose — the ViewModel stays alive and can be resumed
    /// without creating a new instance.
    /// </summary>
    public void Suspend()
    {
        DetachEvents();
    }

    /// <summary>
    /// Re-attach to live data events after being suspended.
    /// </summary>
    public void Resume()
    {
        AttachEvents();
        RefreshLocalizedStrings();
        RefreshRates();
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
        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.InvokeAsync(action);
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

    private void ExecuteTogglePin()
    {
        IsPinned = !IsPinned;
        PinStateChanged?.Invoke(this, IsPinned);
    }
}