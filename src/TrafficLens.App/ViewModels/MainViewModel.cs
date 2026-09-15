using System.Windows.Input;
using TrafficLens.App.Commands;
using TrafficLens.App.Services;
using TrafficLens.Core.Localization;

namespace TrafficLens.App.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly FloatingWidgetService _floatingWidgetService;
    private string _windowTitle = "TrafficLens";
    private string _statusMessage = string.Empty;
    private string _applicationsNavLabel = string.Empty;
    private string _dashboardNavLabel = string.Empty;
    private string _connectionsNavLabel = string.Empty;
    private string _historyNavLabel = string.Empty;
    private string _floatingWidgetToggleLabel = string.Empty;
    private bool _isDashboardVisible = true;
    private bool _isApplicationsVisible;
    private bool _isConnectionsVisible;
    private bool _isHistoryVisible;

    public MainViewModel(
        ILocalizationService localization,
        DashboardViewModel dashboard,
        ApplicationsViewModel applications,
        ConnectionsViewModel connections,
        HistoryViewModel history,
        FloatingWidgetService floatingWidgetService)
    {
        _localization = localization;
        Dashboard = dashboard;
        Applications = applications;
        Connections = connections;
        History = history;
        _floatingWidgetService = floatingWidgetService;
        _localization.CultureChanged += OnCultureChanged;
        _floatingWidgetService.IsVisibleChanged += OnIsVisibleChanged;
        RefreshLocalizedStrings();

        SwitchToEnglishCommand = new RelayCommand(() => _localization.SetCulture("en-US"));
        SwitchToPersianCommand = new RelayCommand(() => _localization.SetCulture("fa-IR"));
        ShowDashboardCommand = new RelayCommand(() => SelectPage(Page.Dashboard));
        ShowApplicationsCommand = new RelayCommand(() => SelectPage(Page.Applications));
        ShowConnectionsCommand = new RelayCommand(() => SelectPage(Page.Connections));
        ShowHistoryCommand = new RelayCommand(() => SelectPage(Page.History));
        ToggleFloatingWidgetCommand = new RelayCommand(() => _floatingWidgetService.Toggle());
    }

    public void Dispose() => _floatingWidgetService.IsVisibleChanged -= OnIsVisibleChanged;

    public DashboardViewModel Dashboard { get; }

    public ApplicationsViewModel Applications { get; }

    public ConnectionsViewModel Connections { get; }

    public HistoryViewModel History { get; }

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
        History
    }
}