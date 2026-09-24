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
    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();

        var services = App.Services.Provider;
        _localization = services.GetRequiredService<ILocalizationService>();
        _settings = services.GetRequiredService<ISettingsService>();
        _widgetService = services.GetRequiredService<IFloatingWidgetService>();
        _startupRegistration = services.GetRequiredService<IStartupRegistrationService>();

        LoadFromSettings();
        ApplyLocalization();

        _localization.CultureChanged += OnCultureChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadFromSettings();
        ApplyFlowDirection();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _localization.CultureChanged -= OnCultureChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnCultureChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyLocalization();
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
        AlwaysOnTopText.Text = _localization["AlwaysOnTopLabel"];
        ResetButton.Content = _localization["ResetToDefaultsLabel"];
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
            ShowWidgetToggle.IsOn = _widgetService.IsVisible ||
                GetBool(FloatingWidgetSettings.EnabledKey, defaultValue: false);
            AlwaysOnTopToggle.IsOn = GetBool(FloatingWidgetSettings.AlwaysOnTopKey, defaultValue: true);
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

    private void ShowWidgetToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        if (ShowWidgetToggle.IsOn)
        {
            _widgetService.Show();
        }
        else
        {
            _widgetService.Hide();
        }
    }

    private void AlwaysOnTopToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _widgetService.SetAlwaysOnTop(AlwaysOnTopToggle.IsOn);
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
        _settings.Set(FloatingWidgetSettings.EnabledKey, bool.FalseString);
        _settings.Set(FloatingWidgetSettings.AlwaysOnTopKey, bool.TrueString);
        _settings.Save();

        _startupRegistration.Disable();
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
