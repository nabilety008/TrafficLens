using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.Services;

namespace TrafficLens.WinUI.Pages;

/// <summary>
/// A compact page for the Floating Widget, reachable from the main navigation.
/// </summary>
/// <remarks>
/// This is a view onto the existing <see cref="IFloatingWidgetService"/>, not a
/// second widget implementation and not a second copy of the state. Both switches
/// write through the service, and the service's change events push the result back
/// into the switches, so this page, the quick toggle at the top of Settings, the
/// widget section further down Settings, and the widget's own close button can
/// never disagree. There is no polling and no timer: everything here is event
/// driven, and the service already owns the persisted value.
/// </remarks>
public sealed partial class FloatingWidgetPage : Page
{
    private readonly ILocalizationService _localization;
    private readonly IFloatingWidgetService _widgetService;
    private bool _loading;
    private bool _subscribed;

    public FloatingWidgetPage()
    {
        InitializeComponent();

        var services = App.Services.Provider;
        _localization = services.GetRequiredService<ILocalizationService>();
        _widgetService = services.GetRequiredService<IFloatingWidgetService>();

        ApplyLocalization();
        SyncFromService();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Subscribing here rather than in the constructor keeps the page correct when
    /// the frame caches it and loads it again: <see cref="OnUnloaded"/> detaches the
    /// handlers, so <see cref="OnLoaded"/> has to attach them again. The guard makes
    /// a second load a no-op instead of a double subscription.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Subscribe();
        ApplyFlowDirection();
        SyncFromService();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _localization.CultureChanged += OnCultureChanged;
        _widgetService.EnabledChanged += OnWidgetEnabledChanged;
        _widgetService.IsVisibleChanged += OnWidgetVisibleChanged;
        _widgetService.AlwaysOnTopChanged += OnAlwaysOnTopChanged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        _localization.CultureChanged -= OnCultureChanged;
        _widgetService.EnabledChanged -= OnWidgetEnabledChanged;
        _widgetService.IsVisibleChanged -= OnWidgetVisibleChanged;
        _widgetService.AlwaysOnTopChanged -= OnAlwaysOnTopChanged;
        _subscribed = false;
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        ApplyLocalization();
        ApplyFlowDirection();
    }

    /// <summary>
    /// The widget can also be turned off from the widget's own close button, so both
    /// this page and Settings follow the service rather than only their own switch.
    /// </summary>
    private void OnWidgetEnabledChanged(object? sender, bool enabled) => SyncFromService();

    private void OnWidgetVisibleChanged(object? sender, EventArgs e) => SyncFromService();

    private void OnAlwaysOnTopChanged(object? sender, bool alwaysOnTop) => SyncFromService();

    private void EnableToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _widgetService.SetEnabled(EnableToggle.IsOn);
        SyncFromService();
    }

    private void AlwaysOnTopToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _widgetService.SetAlwaysOnTop(AlwaysOnTopToggle.IsOn);
        SyncFromService();
    }

    /// <summary>
    /// Pushes the one state the service owns into both switches. <c>_loading</c>
    /// stops the resulting change notifications from bouncing back as user edits.
    /// </summary>
    private void SyncFromService()
    {
        _loading = true;
        try
        {
            EnableToggle.IsOn = WidgetToggleSync.Resolve(_widgetService.IsEnabled, _widgetService.IsVisible);
            AlwaysOnTopToggle.IsOn = _widgetService.IsAlwaysOnTop;
        }
        finally
        {
            _loading = false;
        }
    }

    private void ApplyLocalization()
    {
        PageTitleText.Text = _localization["FloatingWidgetLabel"];
        DescriptionText.Text = _localization["FloatingWidgetPageDescriptionLabel"];
        EnableText.Text = _localization["EnableFloatingWidgetLabel"];
        AlwaysOnTopText.Text = _localization["AlwaysOnTopLabel"];
    }

    private void ApplyFlowDirection()
    {
        PageRoot.FlowDirection = _localization.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }
}
