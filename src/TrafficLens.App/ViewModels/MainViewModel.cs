using System.Windows.Input;
using TrafficLens.App.Commands;
using TrafficLens.Core.Localization;

namespace TrafficLens.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private string _windowTitle = "TrafficLens";
    private string _statusMessage = string.Empty;
    private string _applicationsNavLabel = string.Empty;
    private string _dashboardNavLabel = string.Empty;
    private string _connectionsNavLabel = string.Empty;
    private bool _isDashboardVisible = true;
    private bool _isApplicationsVisible;
    private bool _isConnectionsVisible;

    public MainViewModel(
        ILocalizationService localization,
        DashboardViewModel dashboard,
        ApplicationsViewModel applications,
        ConnectionsViewModel connections)
    {
        _localization = localization;
        Dashboard = dashboard;
        Applications = applications;
        Connections = connections;
        _localization.CultureChanged += (_, _) => RefreshLocalizedStrings();
        RefreshLocalizedStrings();

        SwitchToEnglishCommand = new RelayCommand(() => _localization.SetCulture("en-US"));
        SwitchToPersianCommand = new RelayCommand(() => _localization.SetCulture("fa-IR"));
        ShowDashboardCommand = new RelayCommand(() =>
        {
            IsDashboardVisible = true;
            IsApplicationsVisible = false;
            IsConnectionsVisible = false;
        });
        ShowApplicationsCommand = new RelayCommand(() =>
        {
            IsDashboardVisible = false;
            IsApplicationsVisible = true;
            IsConnectionsVisible = false;
        });
        ShowConnectionsCommand = new RelayCommand(() =>
        {
            IsDashboardVisible = false;
            IsApplicationsVisible = false;
            IsConnectionsVisible = true;
        });
    }

    public DashboardViewModel Dashboard { get; }

    public ApplicationsViewModel Applications { get; }

    public ConnectionsViewModel Connections { get; }

    public string ConnectionsNavLabel
    {
        get => _connectionsNavLabel;
        private set => SetProperty(ref _connectionsNavLabel, value);
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

    private void RefreshLocalizedStrings()
    {
        WindowTitle = _localization["WindowTitle"];
        StatusMessage = _localization["StatusReady"];
        ApplicationsNavLabel = _localization["ApplicationsLabel"];
        DashboardNavLabel = _localization["DashboardLabel"];
        ConnectionsNavLabel = _localization["ConnectionsLabel"];
    }
}