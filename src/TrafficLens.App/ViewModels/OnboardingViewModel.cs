using System.Windows.Input;
using TrafficLens.App.Commands;
using TrafficLens.App.Services;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;

namespace TrafficLens.App.ViewModels;

/// <summary>
/// Backs the first-run Get Started experience (TL-019). A lightweight,
/// dismissible overlay that explains what TrafficLens does and how tunnels,
/// Applications/Administrator, tray, widget, alerts and languages behave.
/// Completion is persisted via ISettingsService; a manual reopen (About /
/// Settings) always remains available and never resets persisted state.
/// </summary>
public sealed class OnboardingViewModel : ViewModelBase, IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;

    private bool _isVisible;
    private string _getStartedTitleLabel = string.Empty;
    private string _getStartedIntroLabel = string.Empty;
    private string _applicationsTopicLabel = string.Empty;
    private string _applicationsTopicText = string.Empty;
    private string _tunnelsTopicLabel = string.Empty;
    private string _tunnelsTopicText = string.Empty;
    private string _trayTopicLabel = string.Empty;
    private string _trayTopicText = string.Empty;
    private string _widgetTopicLabel = string.Empty;
    private string _widgetTopicText = string.Empty;
    private string _alertsTopicLabel = string.Empty;
    private string _alertsTopicText = string.Empty;
    private string _languageTopicLabel = string.Empty;
    private string _languageTopicText = string.Empty;
    private string _dismissLabel = string.Empty;

    public OnboardingViewModel(ILocalizationService localization, ISettingsService settings)
    {
        _localization = localization;
        _settings = settings;

        DismissCommand = new RelayCommand(DismissWhenVisible);

        _localization.CultureChanged += OnCultureChanged;
        RefreshLocalizedStrings();
    }

    public void Dispose()
    {
        _localization.CultureChanged -= OnCultureChanged;
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    public ICommand DismissCommand { get; }

    public string GetStartedTitleLabel
    {
        get => _getStartedTitleLabel;
        private set => SetProperty(ref _getStartedTitleLabel, value);
    }

    public string GetStartedIntroLabel
    {
        get => _getStartedIntroLabel;
        private set => SetProperty(ref _getStartedIntroLabel, value);
    }

    public string ApplicationsTopicLabel
    {
        get => _applicationsTopicLabel;
        private set => SetProperty(ref _applicationsTopicLabel, value);
    }

    public string ApplicationsTopicText
    {
        get => _applicationsTopicText;
        private set => SetProperty(ref _applicationsTopicText, value);
    }

    public string TunnelsTopicLabel
    {
        get => _tunnelsTopicLabel;
        private set => SetProperty(ref _tunnelsTopicLabel, value);
    }

    public string TunnelsTopicText
    {
        get => _tunnelsTopicText;
        private set => SetProperty(ref _tunnelsTopicText, value);
    }

    public string TrayTopicLabel
    {
        get => _trayTopicLabel;
        private set => SetProperty(ref _trayTopicLabel, value);
    }

    public string TrayTopicText
    {
        get => _trayTopicText;
        private set => SetProperty(ref _trayTopicText, value);
    }

    public string WidgetTopicLabel
    {
        get => _widgetTopicLabel;
        private set => SetProperty(ref _widgetTopicLabel, value);
    }

    public string WidgetTopicText
    {
        get => _widgetTopicText;
        private set => SetProperty(ref _widgetTopicText, value);
    }

    public string AlertsTopicLabel
    {
        get => _alertsTopicLabel;
        private set => SetProperty(ref _alertsTopicLabel, value);
    }

    public string AlertsTopicText
    {
        get => _alertsTopicText;
        private set => SetProperty(ref _alertsTopicText, value);
    }

    public string LanguageTopicLabel
    {
        get => _languageTopicLabel;
        private set => SetProperty(ref _languageTopicLabel, value);
    }

    public string LanguageTopicText
    {
        get => _languageTopicText;
        private set => SetProperty(ref _languageTopicText, value);
    }

    public string DismissLabel
    {
        get => _dismissLabel;
        private set => SetProperty(ref _dismissLabel, value);
    }

    /// <summary>
    /// Reopens the panel at any time (manual "Get Started" surface). This is a
    /// pure view action: it never clears or rewrites persisted state.
    /// </summary>
    public void Show() => IsVisible = true;

    /// <summary>
    /// Shows the panel only on the first run of a brand-new profile. Existing
    /// profiles (settings file already present) are treated as completed.
    /// </summary>
    public void AutoShowIfRequired()
    {
        if (OnboardingSettings.ShouldAutoShow(_settings))
        {
            Show();
        }
    }

    private void DismissWhenVisible()
    {
        if (!IsVisible)
        {
            return;
        }

        OnboardingSettings.MarkCompleted(_settings);
        _settings.Save();
        IsVisible = false;
    }

    private void OnCultureChanged(object? sender, EventArgs e) => RefreshLocalizedStrings();

    private void RefreshLocalizedStrings()
    {
        GetStartedTitleLabel = _localization["GetStartedTitle"];
        GetStartedIntroLabel = _localization["GetStartedIntro"];
        ApplicationsTopicLabel = _localization["GetStartedApplicationsTitle"];
        ApplicationsTopicText = _localization["GetStartedApplicationsText"];
        TunnelsTopicLabel = _localization["GetStartedTunnelsTitle"];
        TunnelsTopicText = _localization["GetStartedTunnelsText"];
        TrayTopicLabel = _localization["GetStartedTrayTitle"];
        TrayTopicText = _localization["GetStartedTrayText"];
        WidgetTopicLabel = _localization["GetStartedWidgetTitle"];
        WidgetTopicText = _localization["GetStartedWidgetText"];
        AlertsTopicLabel = _localization["GetStartedAlertsTitle"];
        AlertsTopicText = _localization["GetStartedAlertsText"];
        LanguageTopicLabel = _localization["GetStartedLanguageTitle"];
        LanguageTopicText = _localization["GetStartedLanguageText"];
        DismissLabel = _localization["GetStartedDismiss"];
    }
}