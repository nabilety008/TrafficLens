using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using TrafficLens.App.Services;
using TrafficLens.Core.Alerts;
using TrafficLens.Core.Localization;

namespace TrafficLens.App.ViewModels;

public sealed class AlertsViewModel : ViewModelBase, IDisposable
{
    private readonly IAlertService _alertService;
    private readonly ILocalizationService _localization;
    private readonly Dispatcher? _dispatcher;

    private string _alertsNavLabel = string.Empty;
    private string _alertsTitleLabel = string.Empty;
    private string _noAlertsText = string.Empty;
    private string _alertCountTemplate = string.Empty;
    private string _countText = string.Empty;

    private readonly ObservableCollection<AlertRowViewModel> _recentAlerts = new();

    public AlertsViewModel(IAlertService alertService, ILocalizationService localization)
    {
        _alertService = alertService;
        _localization = localization;
        _dispatcher = Application.Current?.Dispatcher;

        _alertService.AlertRaised += OnAlertRaised;
        _localization.CultureChanged += OnCultureChanged;

        RefreshLocalizedStrings();
        RebuildList();
    }

    public string AlertsNavLabel
    {
        get => _alertsNavLabel;
        private set => SetProperty(ref _alertsNavLabel, value);
    }

    public string AlertsTitleLabel
    {
        get => _alertsTitleLabel;
        private set => SetProperty(ref _alertsTitleLabel, value);
    }

    public string NoAlertsText
    {
        get => _noAlertsText;
        private set => SetProperty(ref _noAlertsText, value);
    }

    public string AlertCountText
    {
        get => _countText;
        private set => SetProperty(ref _countText, value);
    }

    public ObservableCollection<AlertRowViewModel> RecentAlerts => _recentAlerts;

    public bool HasNoAlerts => _recentAlerts.Count == 0;

    public void Dispose()
    {
        _alertService.AlertRaised -= OnAlertRaised;
        _localization.CultureChanged -= OnCultureChanged;
    }

    private void OnAlertRaised(object? sender, AlertRaisedEventArgs e) =>
        RunOnUi(() =>
        {
            _recentAlerts.Insert(0, new AlertRowViewModel(_localization, e.Alert));
            OnPropertyChanged(nameof(HasNoAlerts));
            AlertCountText = FormatCount();
        });

    private void OnCultureChanged(object? sender, EventArgs e) =>
        RunOnUi(() =>
        {
            RefreshLocalizedStrings();
            RebuildList();
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

    private void RebuildList()
    {
        _recentAlerts.Clear();
        foreach (var alert in _alertService.RecentAlerts.Reverse())
        {
            _recentAlerts.Add(new AlertRowViewModel(_localization, alert));
        }

        OnPropertyChanged(nameof(HasNoAlerts));
        AlertCountText = FormatCount();
    }

    private string FormatCount()
    {
        var count = _recentAlerts.Count;
        return count == 0
            ? string.Empty
            : string.Format(_localization.CurrentCulture, _alertCountTemplate, count);
    }

    private void RefreshLocalizedStrings()
    {
        AlertsNavLabel = _localization["AlertsNavLabel"];
        AlertsTitleLabel = _localization["AlertsTitleLabel"];
        NoAlertsText = _localization["AlertsNoAlertsLabel"];
        _alertCountTemplate = _localization["AlertsCountFormat"];
    }
}

public sealed class AlertRowViewModel
{
    private readonly ILocalizationService _localization;
    private readonly AlertEvent _alert;

    public AlertRowViewModel(ILocalizationService localization, AlertEvent alert)
    {
        _localization = localization;
        _alert = alert;
        Message = AlertMessageFormatter.Message(localization, alert);
        TimeText = alert.OccurredUtc.ToLocalTime().ToString("HH:mm:ss");
    }

    public string Message { get; }

    public string TimeText { get; }

    public string TypeText => _localization[AlertTypeKey(_alert.Type)];

    private static string AlertTypeKey(AlertType type) => type switch
    {
        AlertType.HighDownloadSpeed => "AlertTypeHighDownloadSpeed",
        AlertType.HighUploadSpeed => "AlertTypeHighUploadSpeed",
        AlertType.DailyDownloadLimit => "AlertTypeDailyDownloadLimit",
        AlertType.DailyUploadLimit => "AlertTypeDailyUploadLimit",
        AlertType.DailyTotalLimit => "AlertTypeDailyTotalLimit",
        _ => "AlertTypeUnknown"
    };
}