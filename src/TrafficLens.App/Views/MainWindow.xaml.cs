using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;

namespace TrafficLens.App.Views;

public partial class MainWindow : Window
{
    private readonly ILocalizationService _localization;
    private readonly ISystemTrayService _trayService;
    private readonly IFloatingWidgetService _floatingWidgetService;
    private readonly ISettingsService _settings;
    private readonly ApplicationExitCoordinator _exitCoordinator;
    private readonly MainViewModel _viewModel;

    public MainWindow(
        MainViewModel viewModel,
        ILocalizationService localization,
        ISystemTrayService trayService,
        IFloatingWidgetService floatingWidgetService,
        ISettingsService settings,
        ApplicationExitCoordinator exitCoordinator,
        ApplicationsView applicationsView,
        ConnectionsView connectionsView,
        HistoryView historyView,
        AlertsView alertsView,
        SettingsView settingsView)
    {
        _localization = localization;
        _trayService = trayService;
        _floatingWidgetService = floatingWidgetService;
        _settings = settings;
        _exitCoordinator = exitCoordinator;
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        ApplicationsHost.Content = applicationsView;
        ConnectionsHost.Content = connectionsView;
        HistoryHost.Content = historyView;
        AlertsHost.Content = alertsView;
        SettingsHost.Content = settingsView;
        settingsView.DataContext = viewModel.Settings;

        _localization.CultureChanged += OnCultureChanged;
        UpdateFlowDirection();

        Closing += OnWindowClosing;
        StateChanged += OnWindowStateChanged;
        _trayService.OpenRequested += OnTrayOpenRequested;
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        UpdateFlowDirection();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (TrayBehavior.ResolveCloseAction(_exitCoordinator.IsExitRequested, _settings) == WindowCloseAction.Exit)
        {
            _exitCoordinator.RequestApplicationExit();
            _floatingWidgetService.Dispose();
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _localization.CultureChanged -= OnCultureChanged;
            _trayService.OpenRequested -= OnTrayOpenRequested;
            return;
        }

        e.Cancel = true;
        Hide();
        _trayService.ShowFirstCloseToTrayNotice();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized || !TrayBehavior.GetMinimizeToTray(_settings))
        {
            return;
        }

        WindowState = WindowState.Normal;
        Hide();
    }

    private void OnTrayOpenRequested(object? sender, EventArgs e)
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    private void UpdateFlowDirection()
    {
        FlowDirection = _localization.IsRightToLeft
            ? System.Windows.FlowDirection.RightToLeft
            : System.Windows.FlowDirection.LeftToRight;
    }
}