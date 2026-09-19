using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using TrafficLens.App.Infrastructure;
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
    private readonly IServiceProvider _services;

    public MainWindow(
        MainViewModel viewModel,
        ILocalizationService localization,
        ISystemTrayService trayService,
        IFloatingWidgetService floatingWidgetService,
        ISettingsService settings,
        ApplicationExitCoordinator exitCoordinator,
        IServiceProvider services)
    {
        _localization = localization;
        _trayService = trayService;
        _floatingWidgetService = floatingWidgetService;
        _settings = settings;
        _exitCoordinator = exitCoordinator;
        _viewModel = viewModel;
        _services = services;
        StartupTrace.Tick("mainwindow-ctor-begin");
        InitializeComponent();
        StartupTrace.Tick("mainwindow-initializecomponent");
        DataContext = viewModel;

        _localization.CultureChanged += OnCultureChanged;
        UpdateFlowDirection();
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += (_, _) => StartupTrace.Tick("first-render-ready");

        Closing += OnWindowClosing;
        StateChanged += OnWindowStateChanged;
        _trayService.OpenRequested += OnTrayOpenRequested;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsApplicationsVisible) when _viewModel.IsApplicationsVisible:
                EnsurePage(PageSlot.Applications);
                break;
            case nameof(MainViewModel.IsConnectionsVisible) when _viewModel.IsConnectionsVisible:
                EnsurePage(PageSlot.Connections);
                break;
            case nameof(MainViewModel.IsHistoryVisible) when _viewModel.IsHistoryVisible:
                EnsurePage(PageSlot.History);
                break;
            case nameof(MainViewModel.IsAlertsVisible) when _viewModel.IsAlertsVisible:
                EnsurePage(PageSlot.Alerts);
                break;
            case nameof(MainViewModel.IsSettingsVisible) when _viewModel.IsSettingsVisible:
                EnsurePage(PageSlot.Settings);
                break;
            case nameof(MainViewModel.IsAboutVisible) when _viewModel.IsAboutVisible:
                EnsurePage(PageSlot.About);
                break;
        }
    }

    private void EnsurePage(PageSlot slot)
    {
        var host = slot switch
        {
            PageSlot.Applications => ApplicationsHost,
            PageSlot.Connections => ConnectionsHost,
            PageSlot.History => HistoryHost,
            PageSlot.Alerts => AlertsHost,
            PageSlot.Settings => SettingsHost,
            _ => AboutHost
        };

        if (host.Content is not null)
        {
            return;
        }

        switch (slot)
        {
            case PageSlot.Applications:
                host.Content = _services.GetRequiredService<ApplicationsView>();
                break;
            case PageSlot.Connections:
                host.Content = _services.GetRequiredService<ConnectionsView>();
                break;
            case PageSlot.History:
                host.Content = _services.GetRequiredService<HistoryView>();
                break;
            case PageSlot.Alerts:
                host.Content = _services.GetRequiredService<AlertsView>();
                break;
            case PageSlot.Settings:
                var settingsView = _services.GetRequiredService<SettingsView>();
                settingsView.DataContext = _viewModel.Settings;
                host.Content = settingsView;
                break;
            case PageSlot.About:
                var aboutView = _services.GetRequiredService<AboutView>();
                aboutView.DataContext = _viewModel.About;
                host.Content = aboutView;
                break;
        }
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
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
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

    private enum PageSlot
    {
        Applications,
        Connections,
        History,
        Alerts,
        Settings,
        About
    }
}