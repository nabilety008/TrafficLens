using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
    private const int MaxCaptionSafeAreaAttempts = 3;

    private int _captionSafeAreaAttempts;
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;
    private readonly ISystemTrayService _trayService;
    private readonly IFloatingWidgetService _widgetService;
    private readonly ApplicationExitCoordinator _exitCoordinator;
    private bool _navigating;
    private bool _syncingWidgetToggle;
    private bool _isActiveWindow = true;

    public MainWindow(
        ILocalizationService localization,
        ISettingsService settings,
        ISystemTrayService trayService,
        IFloatingWidgetService widgetService,
        ApplicationExitCoordinator exitCoordinator)
    {
        InitializeComponent();
        _localization = localization;
        _settings = settings;
        _trayService = trayService;
        _widgetService = widgetService;
        _exitCoordinator = exitCoordinator;

        var appWindow = AppWindow;
        appWindow.Resize(new SizeInt32(900, 560));
        WindowIcon.Apply(this);
        ApplyCaptionColors(appWindow);

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        ApplyCaptionSafeArea();

        appWindow.Closing += OnAppWindowClosing;
        appWindow.Changed += OnAppWindowChanged;

        _trayService.OpenRequested += OnOpenRequested;
        _localization.CultureChanged += OnCultureChanged;
        RootGrid.ActualThemeChanged += OnActualThemeChanged;
        Activated += OnWindowActivated;

        // The shell control is a view of the one widget-enabled state owned by the
        // service, so it follows the Settings switches and the widget's own close
        // button, and it survives a restart because the value is already persisted.
        _widgetService.EnabledChanged += OnWidgetEnabledChanged;
        _widgetService.IsVisibleChanged += OnWidgetVisibleChanged;

        ApplyLocalization();
        SyncWidgetQuickToggle();

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
    /// Reserves the caption-button area for the title on the edge the shell actually
    /// put the buttons on. The insets come from the live window, or from the window's
    /// own controls when the shell reports none, so this is caption geometry rather
    /// than a fixed margin, and it holds for both layout directions, for any title
    /// length and in both the normal and the maximized state.
    /// </summary>
    /// <remarks>
    /// The reserve is applied to the title rather than as padding on the whole title
    /// bar grid, so the widget quick action in the first column keeps a fixed
    /// physical top-left position instead of shifting with the caption side.
    /// </remarks>
    private void ApplyCaptionSafeArea()
    {
        if (TryApplyCaptionSafeArea() || _captionSafeAreaAttempts >= MaxCaptionSafeAreaAttempts)
        {
            _captionSafeAreaAttempts = 0;
            return;
        }

        _captionSafeAreaAttempts++;
        CompositionTarget.Rendering += ReapplyCaptionSafeAreaOnNextFrame;
    }

    /// <summary>
    /// Asks the caption area again once, on the next frame the compositor produces, and
    /// stops listening. A queued dispatcher item would run inside the frame that is
    /// already in flight and see the same stale zones; the next rendered frame is the
    /// first point at which the compositor has published the new ones. The handler is
    /// removed as it runs and the attempt count is capped, so this is a bounded wait for
    /// a single frame and not a per-frame listener, a timer or a poller.
    /// </summary>
    private void ReapplyCaptionSafeAreaOnNextFrame(object? sender, object args)
    {
        CompositionTarget.Rendering -= ReapplyCaptionSafeAreaOnNextFrame;
        ApplyCaptionSafeArea();
    }

    /// <summary>
    /// Applies the reserve and reports whether the window's caption controls were
    /// readable. The zones are published a frame after a resize, so a false result means
    /// the answer is not available yet rather than that there are no controls, and the
    /// caller asks again behind the frame that has to be produced first. The count is
    /// capped and resets as soon as an attempt succeeds, so this is a bounded deferral
    /// and not a loop, a timer or a poller.
    /// </summary>
    private bool TryApplyCaptionSafeArea()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var dpi = GetDpiForWindow(hwnd);
        var scale = TitleBarCaptionLayout.ScaleFromDpi(dpi);
        var isRightToLeft = RootGrid.FlowDirection == FlowDirection.RightToLeft;
        var leadingInset = AppWindow.TitleBar.LeftInset;
        var trailingInset = AppWindow.TitleBar.RightInset;

        // A reported inset is not proof of a reserved caption area: the shell has been
        // seen reporting none at all for a window that still draws the three controls,
        // and reporting only the resize frame, which reserves a strip far narrower than
        // the controls. The controls are therefore read back from the window itself, on
        // whichever physical side they really are, and the insets are used only where
        // they already cover them.
        var controlsFound = NativeCaptionButtons.TryGetInsets(hwnd, dpi, out var leftInsetPixels, out var rightInsetPixels);

        // The insets arrive in flow order, so the layout direction decides which
        // physical side each one belongs to. Reading them as literal left/right
        // puts the safe area on the wrong edge in a right-to-left window.
        var (padding, usedMeasuredControls) = TitleBarCaptionLayout.Resolve(
            leadingInsetPixels: leadingInset,
            trailingInsetPixels: trailingInset,
            measuredLeftInsetPixels: controlsFound ? leftInsetPixels : null,
            measuredRightInsetPixels: controlsFound ? rightInsetPixels : null,
            dpiScale: scale,
            isRightToLeft: isRightToLeft);

        AppTitleBar.Padding = new Thickness(0);

        // A measured reserve already names the physical edge the controls were found
        // on, so it is applied where it was measured and the title stays clear of them
        // in either language. Shell insets are in flow order instead, and still need
        // the layout direction to be mapped onto the physical edges.
        TitleText.Margin = usedMeasuredControls
            ? new Thickness(padding.Left, 0, padding.Right, 0)
            : isRightToLeft
                ? new Thickness(0, 0, padding.Right, 0)
                : new Thickness(padding.Left, 0, 0, 0);

        return controlsFound;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    /// <summary>
    /// A direct show/hide of the widget. The toggle has already flipped, so the new
    /// value is pushed through the service, which persists the existing setting and
    /// shows or hides the window. No second flag and no polling is involved.
    /// </summary>
    private void WidgetQuickToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_syncingWidgetToggle)
        {
            return;
        }

        _widgetService.SetEnabled(WidgetQuickToggle.IsChecked == true);
        SyncWidgetQuickToggle();
    }

    private void OnWidgetEnabledChanged(object? sender, bool enabled) => SyncWidgetQuickToggle();

    private void OnWidgetVisibleChanged(object? sender, EventArgs e) => SyncWidgetQuickToggle();

    /// <summary>
    /// Shows the single widget-enabled state on the shell control. The guard stops
    /// the assignment from being read back as a user click.
    /// </summary>
    private void SyncWidgetQuickToggle()
    {
        _syncingWidgetToggle = true;
        try
        {
            WidgetQuickToggle.IsChecked = WidgetToggleSync.Resolve(
                _widgetService.IsEnabled,
                _widgetService.IsVisible);
        }
        finally
        {
            _syncingWidgetToggle = false;
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

    /// <summary>
    /// Applies the theme-correct colors to the real Windows caption buttons.
    /// The window draws its own background behind them (extends-content-into-
    /// title-bar), so the system defaults stop matching and the glyphs can turn
    /// black on a dark title bar. The palette is the single decision point and
    /// the window re-applies it when the theme or the activation state changes;
    /// no polling is involved.
    /// </summary>
    private void ApplyCaptionColors(Microsoft.UI.Windowing.AppWindow appWindow)
    {
        var isDark = RootGrid.ActualTheme == ElementTheme.Dark;
        var isActive = _isActiveWindow;
        var colors = CaptionButtonPalette.Resolve(isDark, isActive);
        var titleBar = appWindow.TitleBar;

        titleBar.ButtonForegroundColor = colors.Foreground;
        titleBar.ButtonBackgroundColor = colors.Background;
        titleBar.ButtonHoverForegroundColor = colors.HoverForeground;
        titleBar.ButtonHoverBackgroundColor = colors.HoverBackground;
        titleBar.ButtonPressedForegroundColor = colors.PressedForeground;
        titleBar.ButtonPressedBackgroundColor = colors.PressedBackground;
        titleBar.ButtonInactiveForegroundColor = colors.InactiveForeground;
        titleBar.ButtonInactiveBackgroundColor = colors.InactiveBackground;
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        // The system theme flipped under a theme-following window; recolor with
        // the new effective theme. Pure property writes, no layout involvement.
        ApplyCaptionColors(AppWindow);
    }

    private void OnWindowActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        // The inactive/resting glyph tone differs from the active one, so the
        // palette is re-applied on focus gain and loss. Only fires on real
        // activation transitions, not per frame.
        _isActiveWindow = args.WindowActivationState != Microsoft.UI.Xaml.WindowActivationState.Deactivated;
        ApplyCaptionColors(AppWindow);
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

        var widgetLabel = _localization["FloatingWidgetLabel"];
        WidgetQuickToggleLabel.Text = widgetLabel;
        ToolTipService.SetToolTip(WidgetQuickToggle, widgetLabel);
        AutomationProperties.SetName(WidgetQuickToggle, widgetLabel);

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
        var rightToLeft = _localization.IsRightToLeft;
        RootGrid.FlowDirection = rightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        // The title bar grid is pinned to left-to-right so the widget control keeps
        // the physical top-left corner in both directions. The title therefore sets
        // its own direction and alignment, which keeps it against the navigation
        // edge in Persian exactly as before the control was added.
        TitleText.FlowDirection = rightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
        TitleText.HorizontalAlignment = rightToLeft
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;
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
