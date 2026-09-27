using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.Infrastructure;
using TrafficLens.WinUI.Pages;
using TrafficLens.WinUI.Services;
using Windows.Graphics;
using WinRT.Interop;

namespace TrafficLens.WinUI;

public sealed partial class MainWindow : Window
{
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
        WindowIcon.Apply(this);

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        ApplyCaptionSafeArea();

        appWindow.Closing += OnAppWindowClosing;
        appWindow.Changed += OnAppWindowChanged;

        _trayService.OpenRequested += OnOpenRequested;
        _localization.CultureChanged += OnCultureChanged;
        ApplyLocalization();

        NavView.SelectedItem = DashboardNavItem;
        ContentFrame.Navigate(typeof(DashboardPage));
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
        WindowIcon.Apply(this);
        DispatcherQueue.TryEnqueue(() => WindowIcon.Apply(this));
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
        // The caption-button area is re-resolved whenever the window is resized,
        // maximized/restored or moved to a monitor with a different scale, and it
        // sits on the opposite edge in right-to-left layouts.
        if (args.DidPresenterChange || args.DidSizeChange || args.DidPositionChange)
        {
            ApplyCaptionSafeArea();
        }

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

    /// <summary>
    /// Reserves the caption-button area on the edge the shell actually put it on.
    /// The insets come from the live window, so this is caption geometry rather than
    /// a fixed margin, and it holds for both layout directions, for any title
    /// length and in both the normal and the maximized state.
    /// </summary>
    private void ApplyCaptionSafeArea()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var scale = TitleBarCaptionLayout.ScaleFromDpi(GetDpiForWindow(hwnd));

        // The insets arrive in flow order, so the layout direction decides which
        // physical side each one belongs to. Reading them as literal left/right
        // puts the safe area on the wrong edge in a right-to-left window.
        var (left, right) = TitleBarCaptionLayout.ResolvePadding(
            leadingInsetPixels: AppWindow.TitleBar.LeftInset,
            trailingInsetPixels: AppWindow.TitleBar.RightInset,
            dpiScale: scale,
            isRightToLeft: RootGrid.FlowDirection == FlowDirection.RightToLeft);

        AppTitleBar.Padding = new Thickness(left, 0, right, 0);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

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
        SetNavItem(DashboardNavItem, _localization["DashboardLabel"]);
        SetNavItem(ApplicationsNavItem, _localization["ApplicationsLabel"]);
        SetNavItem(ConnectionsNavItem, _localization["ConnectionsLabel"]);
        SetNavItem(HistoryNavItem, _localization["HistoryLabel"]);
        SetNavItem(AlertsNavItem, _localization["AlertsNavLabel"]);
        SetNavItem(SettingsNavItem, _localization["SettingsNavLabel"]);
        SetNavItem(AboutNavItem, _localization["AboutNavLabel"]);
        ApplyFlowDirection();
        ApplyCaptionSafeArea();
    }

    /// <summary>
    /// Keeps the expanded label, the compact-mode tooltip and the accessibility
    /// name on the same localized text, so the collapsed pane never shows a
    /// leftover or clipped label and the icon-only mode stays accessible.
    /// </summary>
    private static void SetNavItem(NavigationViewItem item, string label)
    {
        item.Content = label;
        ToolTipService.SetToolTip(item, label);
        AutomationProperties.SetName(item, label);
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
    }
}
