using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.Services;

namespace TrafficLens.WinUI.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;
    private readonly IFloatingWidgetService _widgetService;
    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();

        _localization = App.Services.Provider.GetRequiredService<ILocalizationService>();
        _settings = App.Services.Provider.GetRequiredService<ISettingsService>();
        _widgetService = App.Services.Provider.GetRequiredService<IFloatingWidgetService>();

        _loading = true;
        LanguageCombo.SelectedIndex = string.Equals(_settings.Language, "fa-IR", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        MinimizeToTrayToggle.IsOn = TrayBehavior.GetMinimizeToTray(_settings);
        CloseToTrayToggle.IsOn = TrayBehavior.GetCloseToTray(_settings);
        ShowWidgetToggle.IsOn = _widgetService.IsVisible ||
            (_settings.Get(FloatingWidgetSettings.EnabledKey, "false") is var v &&
             bool.TryParse(v, out var enabled) && enabled);
        _loading = false;

        _localization.CultureChanged += OnCultureChanged;
        ApplyLocalization();
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _localization.CultureChanged -= OnCultureChanged;
        Unloaded -= OnUnloaded;
    }

    private void OnCultureChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(ApplyLocalization);

    private void ApplyLocalization()
    {
        PageTitleText.Text = _localization["SettingsNavLabel"];
        GeneralHeaderText.Text = _localization["GeneralLabel"];
        LanguageLabelText.Text = _localization["LanguageLabel"];
        SystemTrayHeaderText.Text = _localization["SystemTrayLabel"];
        MinimizeToTrayToggle.Header = _localization["MinimizeToTrayLabel"];
        CloseToTrayToggle.Header = _localization["CloseToTrayLabel"];
        WidgetHeaderText.Text = _localization["WidgetLabel"];
        ShowWidgetToggle.Header = _localization["EnableFloatingWidgetLabel"];
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

    private void MinimizeToTrayToggle_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.Set(TrayBehavior.MinimizeToTrayKey, MinimizeToTrayToggle.IsOn.ToString());
        _settings.Save();
    }

    private void CloseToTrayToggle_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.Set(TrayBehavior.CloseToTrayKey, CloseToTrayToggle.IsOn.ToString());
        _settings.Save();
    }

    private void ShowWidgetToggle_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
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
}
