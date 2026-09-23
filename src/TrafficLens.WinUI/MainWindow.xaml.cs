using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.Pages;
using TrafficLens.WinUI.Services;
using Windows.Graphics;

namespace TrafficLens.WinUI;

public sealed partial class MainWindow : Window
{
    private const double ExpandedModeThresholdWidth = 900;
    private const double CompactModeThresholdWidth = 640;

    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;
    private readonly ISystemTrayService _trayService;
    private readonly ApplicationExitCoordinator _exitCoordinator;
    private bool _navigating;

    public MainWindow(
        ILocalizationService localization,
        ISettingsService settings,
        ISystemTrayService trayService,
        ApplicationExitCoordinator exitCoordinator)
    {
        InitializeComponent();
        _localization = localization;
        _settings = settings;
        _trayService = trayService;
        _exitCoordinator = exitCoordinator;

        var appWindow = AppWindow;
        appWindow.Resize(new SizeInt32(900, 560));

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        appWindow.Closing += OnAppWindowClosing;
        appWindow.Changed += OnAppWindowChanged;

        _trayService.OpenRequested += OnOpenRequested;
        _localization.CultureChanged += OnCultureChanged;
        RootGrid.SizeChanged += OnRootGridSizeChanged;
        ApplyLocalization();

        NavView.SelectedItem = DashboardNavItem;
        ContentFrame.Navigate(typeof(DashboardPage));
        ApplyPaneDisplayMode(RootGrid.ActualWidth > 0 ? RootGrid.ActualWidth : 900);
    }

    public void ShowMainWindow()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter &&
            presenter.State == OverlappedPresenterState.Minimized)
        {
            presenter.Restore();
        }

        AppWindow.Show();
        Activate();
    }

    private void OnOpenRequested(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(ShowMainWindow);

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        var action = TrayBehavior.ResolveCloseAction(_exitCoordinator.IsExitRequested, _settings);
        if (action == WindowCloseAction.Exit)
        {
            return;
        }

        args.Cancel = true;
        AppWindow.Hide();
        _trayService.ShowFirstCloseToTrayNotice();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (AppWindow.Presenter is not OverlappedPresenter presenter ||
            presenter.State != OverlappedPresenterState.Minimized)
        {
            return;
        }

        if (TrayBehavior.GetMinimizeToTray(_settings))
        {
            AppWindow.Hide();
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyLocalization();
            ApplyFlowDirection();
            PersistLanguage();
        });
    }

    private void ApplyLocalization()
    {
        Title = _localization["WindowTitle"];
        TitleText.Text = _localization["WindowTitle"];
        DashboardNavItem.Content = _localization["DashboardLabel"];
        ApplicationsNavItem.Content = _localization["ApplicationsLabel"];
        ConnectionsNavItem.Content = _localization["ConnectionsLabel"];
        HistoryNavItem.Content = _localization["HistoryLabel"];
        AlertsNavItem.Content = _localization["AlertsNavLabel"];
        SettingsNavItem.Content = _localization["SettingsNavLabel"];
        AboutNavItem.Content = _localization["AboutNavLabel"];
        ApplyFlowDirection();
    }

    private void ApplyFlowDirection()
    {
        RootGrid.FlowDirection = _localization.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    private void PersistLanguage()
    {
        if (!string.Equals(_settings.Language, _localization.CurrentCulture.Name, StringComparison.OrdinalIgnoreCase))
        {
            _settings.Language = _localization.CurrentCulture.Name;
            _settings.Save();
        }
    }

    private void OnRootGridSizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyPaneDisplayMode(e.NewSize.Width);

    private void ApplyPaneDisplayMode(double width)
    {
        var mode = width >= ExpandedModeThresholdWidth
            ? NavigationViewPaneDisplayMode.Left
            : width >= CompactModeThresholdWidth
                ? NavigationViewPaneDisplayMode.LeftCompact
                : NavigationViewPaneDisplayMode.LeftMinimal;

        if (NavView.PaneDisplayMode == mode)
        {
            return;
        }

        NavView.PaneDisplayMode = mode;
        NavView.IsPaneOpen = mode == NavigationViewPaneDisplayMode.Left;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_navigating)
        {
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        var pageType = tag switch
        {
            "Dashboard" => typeof(DashboardPage),
            "Applications" => typeof(ApplicationsPage),
            "Connections" => typeof(ConnectionsPage),
            "History" => typeof(HistoryPage),
            "Alerts" => typeof(AlertsPage),
            "Settings" => typeof(SettingsPage),
            "About" => typeof(AboutPage),
            _ => null
        };

        if (pageType is null)
        {
            return;
        }

        _navigating = true;
        try
        {
            ContentFrame.Navigate(pageType);
        }
        finally
        {
            _navigating = false;
        }

        if (NavView.PaneDisplayMode != NavigationViewPaneDisplayMode.Left)
        {
            NavView.IsPaneOpen = false;
        }
    }
}
