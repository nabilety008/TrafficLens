using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;
using TrafficLens.Infrastructure.Services;
using TrafficLens.WinUI.Services;

namespace TrafficLens.WinUI.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;
    private readonly IFloatingWidgetService _widgetService;
    private readonly IStartupRegistrationService _startupRegistration;
    private readonly IWindowsUpdateService _windowsUpdate;
    private bool _loading;
    private bool _windowsUpdateBusy;
    private WindowsUpdateState _windowsUpdateState = new();
    private WindowsUpdateOperationResult? _windowsUpdateResult;

    public SettingsPage()
    {
        InitializeComponent();

        var services = App.Services.Provider;
        _localization = services.GetRequiredService<ILocalizationService>();
        _settings = services.GetRequiredService<ISettingsService>();
        _widgetService = services.GetRequiredService<IFloatingWidgetService>();
        _startupRegistration = services.GetRequiredService<IStartupRegistrationService>();
        _windowsUpdate = services.GetRequiredService<IWindowsUpdateService>();

        LoadFromSettings();
        ApplyLocalization();

        _localization.CultureChanged += OnCultureChanged;
        _widgetService.EnabledChanged += OnWidgetEnabledChanged;
        _widgetService.IsVisibleChanged += OnWidgetVisibleChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadFromSettings();
        LoadWindowsUpdateState();
        ApplyFlowDirection();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _localization.CultureChanged -= OnCultureChanged;
        _widgetService.EnabledChanged -= OnWidgetEnabledChanged;
        _widgetService.IsVisibleChanged -= OnWidgetVisibleChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    /// <summary>
    /// The widget can also be disabled from the widget's own close button. That
    /// arrives here, and both switches follow, so the page never shows a stale state.
    /// </summary>
    private void OnWidgetEnabledChanged(object? sender, bool enabled) => SyncWidgetToggles();

    private void OnWidgetVisibleChanged(object? sender, EventArgs e) => SyncWidgetToggles();

    private void SyncWidgetToggles()
    {
        var value = WidgetToggleSync.Resolve(_widgetService.IsEnabled, _widgetService.IsVisible);

        _loading = true;
        try
        {
            WidgetToggleSync.Apply(
                isOn => WidgetQuickToggle.IsOn = isOn,
                isOn => ShowWidgetToggle.IsOn = isOn,
                value);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyLocalization();
            LoadWindowsUpdateState();
            ApplyFlowDirection();
        });

    private void ApplyFlowDirection()
    {
        var direction = _localization.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
        PageScroller.FlowDirection = direction;
        RootLayout.FlowDirection = direction;
    }

    private void ApplyLocalization()
    {
        PageTitleText.Text = _localization["SettingsTitleLabel"];
        GeneralHeader.Text = _localization["GeneralLabel"];
        LanguageText.Text = _localization["LanguageLabel"];
        StartupHeader.Text = _localization["StartupLabel"];
        StartWithWindowsText.Text = _localization["StartWithWindowsLabel"];
        StartMinimizedText.Text = _localization["StartMinimizedLabel"];
        TrayHeader.Text = _localization["SystemTrayLabel"];
        MinimizeToTrayText.Text = _localization["MinimizeToTrayLabel"];
        CloseToTrayText.Text = _localization["CloseToTrayLabel"];
        WidgetHeader.Text = _localization["WidgetLabel"];
        ShowWidgetText.Text = _localization["EnableFloatingWidgetLabel"];
        WidgetQuickHeader.Text = _localization["FloatingWidgetLabel"];
        WidgetQuickText.Text = _localization["EnableFloatingWidgetLabel"];
        AlwaysOnTopText.Text = _localization["AlwaysOnTopLabel"];
        ResetButton.Content = _localization["ResetToDefaultsLabel"];

        WindowsUpdateHeader.Text = _localization["WindowsUpdateLabel"];
        WindowsUpdateStatusText.Text = _localization["StatusLabel"];
        WindowsUpdateDisableButton.Content = _localization["WindowsUpdateDisableLabel"];
        WindowsUpdateEnableButton.Content = _localization["WindowsUpdateEnableLabel"];
        RenderWindowsUpdateState();

        if (SavedNoticeText.Visibility == Visibility.Visible)
        {
            SavedNoticeText.Text = _localization["ChangesSavedLabel"];
        }
    }

    private void LoadWindowsUpdateState()
    {
        _windowsUpdateState = _windowsUpdate.GetState();
        RenderWindowsUpdateState();
    }

    private void RenderWindowsUpdateState()
    {
        WindowsUpdateStatusValueText.Text = _windowsUpdateState.Status switch
        {
            WindowsUpdateStatus.Enabled => _localization["WindowsUpdateStatusEnabled"],
            WindowsUpdateStatus.Disabled when _windowsUpdateState.Reason == WindowsUpdateDisableReason.TrafficLens =>
                _localization["WindowsUpdateStatusDisabledByTrafficLens"],
            WindowsUpdateStatus.Disabled when _windowsUpdateState.Reason == WindowsUpdateDisableReason.Service =>
                _localization["WindowsUpdateStatusDisabledByService"],
            WindowsUpdateStatus.Disabled => _localization["WindowsUpdateStatusDisabledByPolicy"],
            WindowsUpdateStatus.ManagedByPolicy => _localization["WindowsUpdateStatusManaged"],
            _ => _localization["WindowsUpdateStatusUnknown"]
        };

        WindowsUpdateNoteText.Text = GetWindowsUpdateNote();
        WindowsUpdateNoteText.Visibility = string.IsNullOrEmpty(WindowsUpdateNoteText.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;

        WindowsUpdateDisableButton.Visibility = _windowsUpdateState.CanDisable
            ? Visibility.Visible
            : Visibility.Collapsed;
        WindowsUpdateEnableButton.Visibility = _windowsUpdateState.CanEnable
            ? Visibility.Visible
            : Visibility.Collapsed;

        var hasAction = _windowsUpdateState.CanDisable || _windowsUpdateState.CanEnable;
        WindowsUpdateWarningText.Text = hasAction ? _localization["WindowsUpdateWarningLabel"] : string.Empty;
        WindowsUpdateWarningText.Visibility = hasAction ? Visibility.Visible : Visibility.Collapsed;

        if (_windowsUpdateResult is { } result)
        {
            WindowsUpdateResultText.Text = result switch
            {
                WindowsUpdateOperationResult.Success => _localization["ChangesSavedLabel"],
                WindowsUpdateOperationResult.Canceled => _localization["WindowsUpdateCanceledLabel"],
                _ => _localization["WindowsUpdateFailedLabel"]
            };
            WindowsUpdateResultText.Visibility = Visibility.Visible;
        }
        else
        {
            WindowsUpdateResultText.Visibility = Visibility.Collapsed;
        }
    }

    private string GetWindowsUpdateNote()
    {
        if (_windowsUpdateState.Status == WindowsUpdateStatus.Unknown)
        {
            return _localization["WindowsUpdateUnknownExplanation"];
        }

        if (_windowsUpdateState.Status == WindowsUpdateStatus.Disabled &&
            _windowsUpdateState.Reason == WindowsUpdateDisableReason.Service)
        {
            return _localization["WindowsUpdateServiceExplanation"];
        }

        if (_windowsUpdateState.Status == WindowsUpdateStatus.ManagedByPolicy ||
            (_windowsUpdateState.Status == WindowsUpdateStatus.Disabled &&
             _windowsUpdateState.Reason == WindowsUpdateDisableReason.Policy))
        {
            return _localization["WindowsUpdateManagedExplanation"];
        }

        if (!_windowsUpdateState.CanDisable && !_windowsUpdateState.CanEnable)
        {
            return _localization["WindowsUpdateUnavailableLabel"];
        }

        return string.Empty;
    }

    private void LoadFromSettings()
    {
        _loading = true;
        try
        {
            LanguageCombo.SelectedIndex =
                string.Equals(_settings.Language, "fa-IR", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            StartWithWindowsToggle.IsOn = GetBool(JsonSettingsService.StartWithWindowsKey, defaultValue: false);
            StartMinimizedToggle.IsOn = GetBool(JsonSettingsService.StartMinimizedKey, defaultValue: false);
            MinimizeToTrayToggle.IsOn = TrayBehavior.GetMinimizeToTray(_settings);
            CloseToTrayToggle.IsOn = TrayBehavior.GetCloseToTray(_settings);
            AlwaysOnTopToggle.IsOn = GetBool(FloatingWidgetSettings.AlwaysOnTopKey, defaultValue: true);

            // Both widget switches show the one value the service owns.
            WidgetToggleSync.Apply(
                isOn => WidgetQuickToggle.IsOn = isOn,
                isOn => ShowWidgetToggle.IsOn = isOn,
                WidgetToggleSync.Resolve(_widgetService.IsEnabled, _widgetService.IsVisible));
        }
        finally
        {
            _loading = false;
        }
    }

    private bool GetBool(string key, bool defaultValue)
    {
        var value = _settings.Get(key, string.Empty);
        return string.IsNullOrEmpty(value) ? defaultValue : bool.TryParse(value, out var parsed) && parsed;
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string culture)
        {
            return;
        }

        if (!string.Equals(_localization.CurrentCulture.Name, culture, StringComparison.OrdinalIgnoreCase))
        {
            _localization.SetCulture(culture);
        }
    }

    private void StartWithWindowsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        var enabled = StartWithWindowsToggle.IsOn;
        _settings.Set(JsonSettingsService.StartWithWindowsKey, enabled.ToString());
        if (enabled)
        {
            _startupRegistration.Enable(StartMinimizedToggle.IsOn);
        }
        else
        {
            _startupRegistration.Disable();
        }

        _settings.Save();
    }

    private void StartMinimizedToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.Set(JsonSettingsService.StartMinimizedKey, StartMinimizedToggle.IsOn.ToString());
        if (StartWithWindowsToggle.IsOn)
        {
            _startupRegistration.Enable(StartMinimizedToggle.IsOn);
        }

        _settings.Save();
    }

    private void MinimizeToTrayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.Set(TrayBehavior.MinimizeToTrayKey, MinimizeToTrayToggle.IsOn.ToString());
        _settings.Save();
    }

    private void CloseToTrayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.Set(TrayBehavior.CloseToTrayKey, CloseToTrayToggle.IsOn.ToString());
        _settings.Save();
    }

    private void ShowWidgetToggle_Toggled(object sender, RoutedEventArgs e) =>
        ApplyWidgetToggle(ShowWidgetToggle.IsOn);

    private void WidgetQuickToggle_Toggled(object sender, RoutedEventArgs e) =>
        ApplyWidgetToggle(WidgetQuickToggle.IsOn);

    /// <summary>
    /// Both switches go through the service, which is the only writer of the widget
    /// setting, and then both are re-synced so the pair can never disagree.
    /// </summary>
    private void ApplyWidgetToggle(bool enabled)
    {
        if (_loading)
        {
            return;
        }

        _widgetService.SetEnabled(enabled);
        SyncWidgetToggles();
    }

    private void AlwaysOnTopToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _widgetService.SetAlwaysOnTop(AlwaysOnTopToggle.IsOn);
    }

    private async void WindowsUpdateDisable_Click(object sender, RoutedEventArgs e)
    {
        if (_windowsUpdateBusy)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _localization["WindowsUpdateLabel"],
            Content = _localization["WindowsUpdateConfirmDisableText"],
            PrimaryButtonText = _localization["WindowsUpdateConfirmLabel"],
            CloseButtonText = _localization["CancelLabel"],
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await RunWindowsUpdateActionAsync(() => _windowsUpdate.DisableAsync());
    }

    private async void WindowsUpdateEnable_Click(object sender, RoutedEventArgs e)
    {
        if (_windowsUpdateBusy)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _localization["WindowsUpdateLabel"],
            Content = _localization["WindowsUpdateConfirmEnableText"],
            PrimaryButtonText = _localization["WindowsUpdateConfirmLabel"],
            CloseButtonText = _localization["CancelLabel"],
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await RunWindowsUpdateActionAsync(() => _windowsUpdate.EnableAsync());
    }

    private async Task RunWindowsUpdateActionAsync(Func<Task<WindowsUpdateOperationResult>> action)
    {
        if (_windowsUpdateBusy)
        {
            return;
        }

        _windowsUpdateBusy = true;
        WindowsUpdateDisableButton.IsEnabled = false;
        WindowsUpdateEnableButton.IsEnabled = false;
        _windowsUpdateResult = null;
        RenderWindowsUpdateState();

        try
        {
            _windowsUpdateResult = await action();
            LoadWindowsUpdateState();
        }
        finally
        {
            _windowsUpdateBusy = false;
            WindowsUpdateDisableButton.IsEnabled = true;
            WindowsUpdateEnableButton.IsEnabled = true;
            RenderWindowsUpdateState();
        }
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _localization["ResetToDefaultsLabel"],
            Content = _localization["ResetAreYouSureLabel"],
            PrimaryButtonText = _localization["ResetConfirmLabel"],
            CloseButtonText = _localization["CancelLabel"],
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        ResetToDefaults();
    }

    private void ResetToDefaults()
    {
        _settings.Set(JsonSettingsService.LanguageKey, "en-US");
        _settings.Set(JsonSettingsService.StartWithWindowsKey, bool.FalseString);
        _settings.Set(JsonSettingsService.StartMinimizedKey, bool.FalseString);
        _settings.Set(TrayBehavior.MinimizeToTrayKey, bool.TrueString);
        _settings.Set(TrayBehavior.CloseToTrayKey, bool.TrueString);
        _settings.Set(FloatingWidgetSettings.AlwaysOnTopKey, bool.TrueString);
        _settings.Save();

        _startupRegistration.Disable();

        // The widget's enabled flag is written through the service only, so the
        // reset cannot leave the switches and the persisted value out of step.
        _widgetService.Hide();
        _widgetService.SetAlwaysOnTop(true);

        LoadFromSettings();

        if (!string.Equals(_localization.CurrentCulture.Name, "en-US", StringComparison.OrdinalIgnoreCase))
        {
            _localization.SetCulture("en-US");
        }

        ApplyLocalization();
        ApplyFlowDirection();
        SavedNoticeText.Text = _localization["ChangesSavedLabel"];
        SavedNoticeText.Visibility = Visibility.Visible;
    }
}
