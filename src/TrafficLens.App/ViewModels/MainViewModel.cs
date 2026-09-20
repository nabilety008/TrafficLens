using System.Windows.Input;
using TrafficLens.App.Commands;
using TrafficLens.App.Infrastructure;
using TrafficLens.App.Services;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;

namespace TrafficLens.App.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;
    private readonly IFloatingWidgetService _floatingWidgetService;
    private string _windowTitle = "TrafficLens";
    private string _statusMessage = string.Empty;
    private string _applicationsNavLabel = string.Empty;
    private string _dashboardNavLabel = string.Empty;
    private string _connectionsNavLabel = string.Empty;
    private string _historyNavLabel = string.Empty;
    private string _alertsNavLabel = string.Empty;
    private string _settingsNavLabel = string.Empty;
    private string _aboutNavLabel = string.Empty;
    private string _floatingWidgetToggleLabel = string.Empty;
    private string _minimizeToTrayLabel = string.Empty;
    private string _closeToTrayLabel = string.Empty;
    private bool _minimizeToTray;
    private bool _closeToTray;
    private bool _isDashboardVisible = true;
    private bool _isApplicationsVisible;
    private bool _isConnectionsVisible;
    private bool _isHistoryVisible;
    private bool _isAlertsVisible;
    private bool _isSettingsVisible;
    private bool _isAboutVisible;

    public MainViewModel(
        ILocalizationService localization,
        ISettingsService settings,
        DashboardViewModel dashboard,
        ApplicationsViewModel applications,
        ConnectionsViewModel connections,
        HistoryViewModel history,
        AlertsViewModel alerts,
        SettingsViewModel settingsPage,
        AboutViewModel about,
        IFloatingWidgetService floatingWidgetService,
        OnboardingViewModel onboarding)
    {
        _localization = localization;
        _settings = settings;
        Dashboard = dashboard;
        Applications = applications;
        Connections = connections;
        History = history;
        Alerts = alerts;
        Settings = settingsPage;
        About = about;
        _floatingWidgetService = floatingWidgetService;
        Onboarding = onboarding;
        StartupTrace.Tick("mainviewmodel-ctor-begin");

        _minimizeToTray = TrayBehavior.GetMinimizeToTray(_settings);
        _closeToTray = TrayBehavior.GetCloseToTray(_settings);

        Connections.SetActive(false);

        _localization.CultureChanged += OnCultureChanged;
        _floatingWidgetService.IsVisibleChanged += OnIsVisibleChanged;
        RefreshLocalizedStrings();

        SwitchToEnglishCommand = new RelayCommand(() => _localization.SetCulture("en-US"));
        SwitchToPersianCommand = new RelayCommand(() => _localization.SetCulture("fa-IR"));
        ShowDashboardCommand = new RelayCommand(() => SelectPage(Page.Dashboard));
        ShowApplicationsCommand = new RelayCommand(() => SelectPage(Page.Applications));
        ShowConnectionsCommand = new RelayCommand(() => SelectPage(Page.Connections));
        ShowHistoryCommand = new RelayCommand(() => SelectPage(Page.History));
        ShowAlertsCommand = new RelayCommand(() => SelectPage(Page.Alerts));
        ShowSettingsCommand = new RelayCommand(() => SelectPage(Page.Settings));
        ShowAboutCommand = new RelayCommand(() => SelectPage(Page.About));
        ToggleFloatingWidgetCommand = new RelayCommand(() => _floatingWidgetService.Toggle());

        Onboarding.AutoShowIfRequired();
    }

    public void Dispose()
    {
        _floatingWidgetService.IsVisibleChanged -= OnIsVisibleChanged;
        About.Dispose();
        Settings.Dispose();
        Onboarding.Dispose();
    }

    public DashboardViewModel Dashboard { get; }

    public ApplicationsViewModel Applications { get; }

    public ConnectionsViewModel Connections { get; }

    public HistoryViewModel History { get; }

    public AlertsViewModel Alerts { get; }

    public SettingsViewModel Settings { get; }

    public AboutViewModel About { get; }

    public OnboardingViewModel Onboarding { get; }

    public string AboutNavLabel
    {
        get => _aboutNavLabel;
        private set => SetProperty(ref _aboutNavLabel, value);
    }

    public string SettingsNavLabel
    {
        get => _settingsNavLabel;
        private set => SetProperty(ref _settingsNavLabel, value);
    }

    public string ConnectionsNavLabel
    {
        get => _connectionsNavLabel;
        private set => SetProperty(ref _connectionsNavLabel, value);
    }

    public string HistoryNavLabel
    {
        get => _historyNavLabel;
        private set => SetProperty(ref _historyNavLabel, value);
    }

    public string AlertsNavLabel
    {
        get => _alertsNavLabel;
        private set => SetProperty(ref _alertsNavLabel, value);
    }

    public string ApplicationsNavLabel
    {
        get => _applicationsNavLabel;
        private set => SetProperty(ref _applicationsNavLabel, value);
    }

    public string DashboardNavLabel
    {
        get => _dashboardNavLabel;
        private set => SetProperty(ref _dashboardNavLabel, value);
    }

    public string MinimizeToTrayLabel
    {
        get => _minimizeToTrayLabel;
        private set => SetProperty(ref _minimizeToTrayLabel, value);
    }

    public string CloseToTrayLabel
    {
        get => _closeToTrayLabel;
        private set => SetProperty(ref _closeToTrayLabel, value);
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set
        {
            if (SetProperty(ref _minimizeToTray, value))
            {
                _settings.Set(TrayBehavior.MinimizeToTrayKey, value.ToString());
                _settings.Save();
            }
        }
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set
        {
            if (SetProperty(ref _closeToTray, value))
            {
                _settings.Set(TrayBehavior.CloseToTrayKey, value.ToString());
                _settings.Save();
            }
        }
    }

    public bool IsConnectionsVisible
    {
        get => _isConnectionsVisible;
        private set => SetProperty(ref _isConnectionsVisible, value);
    }

    public bool IsHistoryVisible
    {
        get => _isHistoryVisible;
        private set => SetProperty(ref _isHistoryVisible, value);
    }

    public bool IsAlertsVisible
    {
        get => _isAlertsVisible;
        private set => SetProperty(ref _isAlertsVisible, value);
    }

    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        private set => SetProperty(ref _isSettingsVisible, value);
    }

    public bool IsDashboardVisible
    {
        get => _isDashboardVisible;
        private set => SetProperty(ref _isDashboardVisible, value);
    }

    public bool IsApplicationsVisible
    {
        get => _isApplicationsVisible;
        private set => SetProperty(ref _isApplicationsVisible, value);
    }

    public bool IsAboutVisible
    {
        get => _isAboutVisible;
        private set => SetProperty(ref _isAboutVisible, value);
    }

    public string WindowTitle
    {
        get => _windowTitle;
        private set => SetProperty(ref _windowTitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand SwitchToEnglishCommand { get; }

    public ICommand SwitchToPersianCommand { get; }

    public ICommand ShowDashboardCommand { get; }

    public ICommand ShowApplicationsCommand { get; }

    public ICommand ShowConnectionsCommand { get; }

    public ICommand ShowHistoryCommand { get; }

    public ICommand ShowAlertsCommand { get; }

    public ICommand ShowSettingsCommand { get; }

    public ICommand ShowAboutCommand { get; }

    public ICommand ToggleFloatingWidgetCommand { get; }

    public string FloatingWidgetToggleLabel
    {
        get => _floatingWidgetToggleLabel;
        private set => SetProperty(ref _floatingWidgetToggleLabel, value);
    }

    private void SelectPage(Page page)
    {
        IsDashboardVisible = page == Page.Dashboard;
        IsApplicationsVisible = page == Page.Applications;
        IsConnectionsVisible = page == Page.Connections;
        IsHistoryVisible = page == Page.History;
        IsAlertsVisible = page == Page.Alerts;
        IsSettingsVisible = page == Page.Settings;
        IsAboutVisible = page == Page.About;

        Connections.SetActive(page == Page.Connections);
    }

    private void OnCultureChanged(object? sender, EventArgs e) => RefreshLocalizedStrings();

    private void OnIsVisibleChanged(object? sender, EventArgs e) => RefreshFloatingWidgetToggleLabel();

    private void RefreshLocalizedStrings()
    {
        WindowTitle = _localization["WindowTitle"];
        StatusMessage = _localization["StatusReady"];
        ApplicationsNavLabel = _localization["ApplicationsLabel"];
        DashboardNavLabel = _localization["DashboardLabel"];
        ConnectionsNavLabel = _localization["ConnectionsLabel"];
        HistoryNavLabel = _localization["HistoryLabel"];
        AlertsNavLabel = _localization["AlertsNavLabel"];
        SettingsNavLabel = _localization["SettingsNavLabel"];
        AboutNavLabel = _localization["AboutNavLabel"];
        MinimizeToTrayLabel = _localization["MinimizeToTrayLabel"];
        CloseToTrayLabel = _localization["CloseToTrayLabel"];
        RefreshFloatingWidgetToggleLabel();
    }

    private void RefreshFloatingWidgetToggleLabel() =>
        FloatingWidgetToggleLabel = _floatingWidgetService.IsVisible
            ? _localization["HideFloatingWidgetLabel"]
            : _localization["ShowFloatingWidgetLabel"];

    private enum Page
    {
        Dashboard,
        Applications,
        Connections,
        History,
        Alerts,
        Settings,
        About
    }
}