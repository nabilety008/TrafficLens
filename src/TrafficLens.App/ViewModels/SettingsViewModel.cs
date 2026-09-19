using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using TrafficLens.App.Commands;
using TrafficLens.App.Services;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Alerts;
using TrafficLens.Core.Localization;
using TrafficLens.Infrastructure.Services;

namespace TrafficLens.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private const int MinCooldownMinutes = 1;
    private const int MaxCooldownMinutes = 1440;

    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;
    private readonly IFloatingWidgetService _floatingWidgetService;
    private readonly IAlertService _alertService;
    private readonly IStartupRegistrationService _startupRegistration;

    private string _loadedLanguage = string.Empty;
    private bool _loadedStartWithWindows;
    private bool _loadedStartMinimized;
    private bool _loadedWidgetEnabled;
    private bool _loadedWidgetAlwaysOnTop;

    private string _language;
    private bool _startWithWindows;
    private bool _startMinimized;
    private bool _minimizeToTray;
    private bool _closeToTray;
    private bool _widgetEnabled;
    private bool _widgetAlwaysOnTop;
    private string _cooldownText;
    private string _validationError = string.Empty;
    private string _savedNotice = string.Empty;
    private bool _isDirty;

    private string _settingsTitle = string.Empty;
    private string _generalLabel = string.Empty;
    private string _languageLabel = string.Empty;
    private string _startWithWindowsLabel = string.Empty;
    private string _startMinimizedLabel = string.Empty;
    private string _systemTrayLabel = string.Empty;
    private string _minimizeToTrayLabel = string.Empty;
    private string _closeToTrayLabel = string.Empty;
    private string _widgetLabel = string.Empty;
    private string _enableFloatingWidgetLabel = string.Empty;
    private string _alwaysOnTopLabel = string.Empty;
    private string _showWidgetLabel = string.Empty;
    private string _hideWidgetLabel = string.Empty;
    private string _alertsLabel = string.Empty;
    private string _cooldownLabel = string.Empty;
    private string _minutesLabel = string.Empty;
    private string _cooldownRangeLabel = string.Empty;
    private string _saveLabel = string.Empty;
    private string _resetToDefaultsLabel = string.Empty;
    private string _getStartedLabel = string.Empty;

    public SettingsViewModel(
        ILocalizationService localization,
        ISettingsService settings,
        IFloatingWidgetService floatingWidgetService,
        IAlertService alertService,
        IStartupRegistrationService startupRegistration,
        OnboardingViewModel onboarding)
    {
        _localization = localization;
        _settings = settings;
        _floatingWidgetService = floatingWidgetService;
        _alertService = alertService;
        _startupRegistration = startupRegistration;

        LanguageOptions = new[]
        {
            new LanguageOption("English", "en-US"),
            new LanguageOption("فارسی", "fa-IR")
        };
        SpeedUnitOptions = new[] { "KB/s", "MB/s", "GB/s" };
        SizeUnitOptions = new[] { "MB", "GB", "TB" };

        foreach (var id in new[] { "highDownloadSpeed", "highUploadSpeed", "dailyDownloadLimit", "dailyUploadLimit", "dailyTotalLimit" })
        {
            var rule = new AlertRuleViewModel(id)
            {
                UnitOptions = IsSpeedRule(id) ? SpeedUnitOptions : SizeUnitOptions
            };
            rule.AttachOnEdited(() => IsDirty = true);
            AlertRules.Add(rule);
        }

        SaveCommand = new RelayCommand(Save);
        ResetCommand = new RelayCommand(ResetWithConfirmation);
        ShowWidgetCommand = new RelayCommand(() => _floatingWidgetService.Show());
        HideWidgetCommand = new RelayCommand(() => _floatingWidgetService.Hide());
        ShowGuideCommand = new RelayCommand(onboarding.Show);

        _language = "en-US";
        _minimizeToTray = true;
        _closeToTray = true;
        _widgetEnabled = false;
        _widgetAlwaysOnTop = true;
        _cooldownText = "5";

        RefreshFromSettings();
        SavedNotice = string.Empty;

        _localization.CultureChanged += OnCultureChanged;
        RefreshLocalizedStrings();
    }

    public ObservableCollection<AlertRuleViewModel> AlertRules { get; } = new();

    public LanguageOption[] LanguageOptions { get; }

    public string[] SpeedUnitOptions { get; }

    public string[] SizeUnitOptions { get; }

    private int _languageIndex;

    public int LanguageIndex
    {
        get => _languageIndex;
        set
        {
            if (SetProperty(ref _languageIndex, value))
            {
                IsDirty = true;
            }
        }
    }

    public string Language => LanguageIndex == 1 ? "fa-IR" : "en-US";

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (SetProperty(ref _startWithWindows, value))
            {
                IsDirty = true;
            }
        }
    }

    public bool StartMinimized
    {
        get => _startMinimized;
        set
        {
            if (SetProperty(ref _startMinimized, value))
            {
                IsDirty = true;
            }
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set
        {
            if (SetProperty(ref _minimizeToTray, value))
            {
                _settings.Set(TrayBehavior.MinimizeToTrayKey, value.ToString());
                _settings.Save();
            }
        }
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set
        {
            if (SetProperty(ref _closeToTray, value))
            {
                _settings.Set(TrayBehavior.CloseToTrayKey, value.ToString());
                _settings.Save();
            }
        }
    }

    public bool WidgetEnabled
    {
        get => _widgetEnabled;
        set
        {
            if (SetProperty(ref _widgetEnabled, value))
            {
                IsDirty = true;
            }
        }
    }

    public bool WidgetAlwaysOnTop
    {
        get => _widgetAlwaysOnTop;
        set
        {
            if (SetProperty(ref _widgetAlwaysOnTop, value))
            {
                IsDirty = true;
            }
        }
    }

    public string CooldownText
    {
        get => _cooldownText;
        set
        {
            if (SetProperty(ref _cooldownText, value))
            {
                IsDirty = true;
            }
        }
    }

    public string ValidationError
    {
        get => _validationError;
        private set
        {
            if (SetProperty(ref _validationError, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }

    public string SavedNotice
    {
        get => _savedNotice;
        private set
        {
            if (SetProperty(ref _savedNotice, value))
            {
                OnPropertyChanged(nameof(HasSavedNotice));
            }
        }
    }

    public bool HasValidationError => !string.IsNullOrEmpty(_validationError);

    public bool HasSavedNotice => !string.IsNullOrEmpty(_savedNotice);

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    public ICommand SaveCommand { get; }

    public ICommand ResetCommand { get; }

    public ICommand ShowWidgetCommand { get; }

    public ICommand HideWidgetCommand { get; }

    public ICommand ShowGuideCommand { get; }

    public string GetStartedLabel
    {
        get => _getStartedLabel;
        private set => SetProperty(ref _getStartedLabel, value);
    }

    public string SettingsTitle
    {
        get => _settingsTitle;
        private set => SetProperty(ref _settingsTitle, value);
    }

    public string GeneralLabel
    {
        get => _generalLabel;
        private set => SetProperty(ref _generalLabel, value);
    }

    public string LanguageLabel
    {
        get => _languageLabel;
        private set => SetProperty(ref _languageLabel, value);
    }

    public string StartWithWindowsLabel
    {
        get => _startWithWindowsLabel;
        private set => SetProperty(ref _startWithWindowsLabel, value);
    }

    public string StartMinimizedLabel
    {
        get => _startMinimizedLabel;
        private set => SetProperty(ref _startMinimizedLabel, value);
    }

    public string SystemTrayLabel
    {
        get => _systemTrayLabel;
        private set => SetProperty(ref _systemTrayLabel, value);
    }

    public string MinimizeToTrayLabel
    {
        get => _minimizeToTrayLabel;
        private set => SetProperty(ref _minimizeToTrayLabel, value);
    }

    public string CloseToTrayLabel
    {
        get => _closeToTrayLabel;
        private set => SetProperty(ref _closeToTrayLabel, value);
    }

    public string WidgetLabel
    {
        get => _widgetLabel;
        private set => SetProperty(ref _widgetLabel, value);
    }

    public string EnableFloatingWidgetLabel
    {
        get => _enableFloatingWidgetLabel;
        private set => SetProperty(ref _enableFloatingWidgetLabel, value);
    }

    public string AlwaysOnTopLabel
    {
        get => _alwaysOnTopLabel;
        private set => SetProperty(ref _alwaysOnTopLabel, value);
    }

    public string ShowWidgetLabel
    {
        get => _showWidgetLabel;
        private set => SetProperty(ref _showWidgetLabel, value);
    }

    public string HideWidgetLabel
    {
        get => _hideWidgetLabel;
        private set => SetProperty(ref _hideWidgetLabel, value);
    }

    public string AlertsLabel
    {
        get => _alertsLabel;
        private set => SetProperty(ref _alertsLabel, value);
    }

    public string CooldownLabel
    {
        get => _cooldownLabel;
        private set => SetProperty(ref _cooldownLabel, value);
    }

    public string MinutesLabel
    {
        get => _minutesLabel;
        private set => SetProperty(ref _minutesLabel, value);
    }

    public string CooldownRangeLabel
    {
        get => _cooldownRangeLabel;
        private set => SetProperty(ref _cooldownRangeLabel, value);
    }

    public string SaveLabel
    {
        get => _saveLabel;
        private set => SetProperty(ref _saveLabel, value);
    }

    public string ResetToDefaultsLabel
    {
        get => _resetToDefaultsLabel;
        private set => SetProperty(ref _resetToDefaultsLabel, value);
    }

    public void Dispose()
    {
        _localization.CultureChanged -= OnCultureChanged;
    }

    public void Save()
    {
        if (!Validate())
        {
            return;
        }

        var config = AlertSettings.Load(_settings);
        var newConfig = BuildAlertConfig();
        var languageChanged = !string.Equals(_loadedLanguage, Language, StringComparison.OrdinalIgnoreCase);
        var widgetEnabledChanged = _loadedWidgetEnabled != WidgetEnabled;
        var widgetAlwaysOnTopChanged = _loadedWidgetAlwaysOnTop != WidgetAlwaysOnTop;
        var alertsChanged = newConfig != config;

        if (languageChanged)
        {
            _localization.SetCulture(Language);
        }

        if (StartWithWindows)
        {
            _startupRegistration.Enable(StartMinimized);
        }
        else
        {
            _startupRegistration.Disable();
        }

        _settings.Set(JsonSettingsService.LanguageKey, Language);
        _settings.Set(JsonSettingsService.StartWithWindowsKey, StartWithWindows.ToString());
        _settings.Set(JsonSettingsService.StartMinimizedKey, StartMinimized.ToString());
        _settings.Set(TrayBehavior.MinimizeToTrayKey, MinimizeToTray.ToString());
        _settings.Set(TrayBehavior.CloseToTrayKey, CloseToTray.ToString());
        _settings.Set(FloatingWidgetSettings.EnabledKey, WidgetEnabled.ToString());
        _settings.Set(FloatingWidgetSettings.AlwaysOnTopKey, WidgetAlwaysOnTop.ToString());

        AlertSettings.Save(_settings, newConfig);

        if (widgetEnabledChanged)
        {
            if (WidgetEnabled)
            {
                _floatingWidgetService.Show();
            }
            else
            {
                _floatingWidgetService.Hide();
            }
        }

        if (widgetAlwaysOnTopChanged)
        {
            _floatingWidgetService.SetAlwaysOnTop(WidgetAlwaysOnTop);
        }

        if (alertsChanged)
        {
            _alertService.RefreshConfig();
        }

        RefreshFromSettings();
        IsDirty = false;
        SavedNotice = _localization["ChangesSavedLabel"];
    }

    private void ResetWithConfirmation()
    {
        var result = MessageBox.Show(
            _localization["ResetAreYouSureLabel"],
            _localization["ResetToDefaultsLabel"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            ResetToDefaults();
        }
    }

    public void ResetToDefaults()
    {
        var defaults = AlertConfig.Default();

        _startWithWindows = false;
        _startMinimized = false;
        _minimizeToTray = true;
        _closeToTray = true;
        _widgetEnabled = false;
        _widgetAlwaysOnTop = true;
        _language = "en-US";
        LanguageIndex = 0;

        foreach (var rule in AlertRules)
        {
            rule.IsEnabled = defaults.IsRuleEnabled(GetAlertType(rule.Id));
            rule.ThresholdText = FormatThreshold(
                defaults.ThresholdOf(GetAlertType(rule.Id)),
                IsSpeedRule(rule.Id));
            rule.UnitIndex = GetDefaultUnitIndex(rule.Id);
        }

        CooldownText = ((int)defaults.Cooldown.TotalMinutes).ToString(CultureInfo.InvariantCulture);
        ValidationError = string.Empty;
        SavedNotice = string.Empty;

        OnPropertyChanged(nameof(StartWithWindows));
        OnPropertyChanged(nameof(StartMinimized));
        OnPropertyChanged(nameof(MinimizeToTray));
        OnPropertyChanged(nameof(CloseToTray));
        OnPropertyChanged(nameof(WidgetEnabled));
        OnPropertyChanged(nameof(WidgetAlwaysOnTop));
        OnPropertyChanged(nameof(Language));
    }

    private bool Validate()
    {
        ValidationError = string.Empty;

        var cooldown = ParseNumber(CooldownText);
        if (double.IsNaN(cooldown) || cooldown < MinCooldownMinutes || cooldown > MaxCooldownMinutes)
        {
            ValidationError = _localization["InvalidValueLabel"];
            return false;
        }

        foreach (var rule in AlertRules)
        {
            var value = ParseNumber(rule.ThresholdText);
            if (double.IsNaN(value) || value <= 0)
            {
                ValidationError = _localization["InvalidValueLabel"];
                return false;
            }
        }

        return true;
    }

    private void RefreshFromSettings()
    {
        var language = _settings.Get(JsonSettingsService.LanguageKey, "en-US");
        _loadedLanguage = language;
        _loadedStartWithWindows = GetBool(JsonSettingsService.StartWithWindowsKey, defaultValue: false);
        _loadedStartMinimized = GetBool(JsonSettingsService.StartMinimizedKey, defaultValue: false);
        _loadedWidgetEnabled = GetBool(FloatingWidgetSettings.EnabledKey, defaultValue: false);
        _loadedWidgetAlwaysOnTop = GetBool(FloatingWidgetSettings.AlwaysOnTopKey, defaultValue: true);

        _language = language;
        _startWithWindows = _loadedStartWithWindows;
        _startMinimized = _loadedStartMinimized;
        _minimizeToTray = TrayBehavior.GetMinimizeToTray(_settings);
        _closeToTray = TrayBehavior.GetCloseToTray(_settings);
        _widgetEnabled = _loadedWidgetEnabled;
        _widgetAlwaysOnTop = _loadedWidgetAlwaysOnTop;

        _languageIndex = language.Equals("fa-IR", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        OnPropertyChanged(nameof(LanguageIndex));
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(StartWithWindows));
        OnPropertyChanged(nameof(StartMinimized));
        OnPropertyChanged(nameof(MinimizeToTray));
        OnPropertyChanged(nameof(CloseToTray));
        OnPropertyChanged(nameof(WidgetEnabled));
        OnPropertyChanged(nameof(WidgetAlwaysOnTop));

        var config = AlertSettings.Load(_settings);
        _cooldownText = ((int)config.Cooldown.TotalMinutes).ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(CooldownText));

        foreach (var rule in AlertRules)
        {
            var type = GetAlertType(rule.Id);
            rule.IsEnabled = config.IsRuleEnabled(type);
            rule.ThresholdText = FormatThreshold(config.ThresholdOf(type), IsSpeedRule(rule.Id));
            rule.UnitIndex = GetUnitIndex(rule.Id, config.ThresholdOf(type));
            rule.UnitOptions = IsSpeedRule(rule.Id) ? SpeedUnitOptions : SizeUnitOptions;
        }

        IsDirty = false;
        ValidationError = string.Empty;
        SavedNotice = string.Empty;
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        ValidationError = string.Empty;
        SavedNotice = string.Empty;
        RefreshLocalizedStrings();
    }

    private void RefreshLocalizedStrings()
    {
        SettingsTitle = _localization["SettingsTitleLabel"];
        GeneralLabel = _localization["GeneralLabel"];
        LanguageLabel = _localization["LanguageLabel"];
        StartWithWindowsLabel = _localization["StartWithWindowsLabel"];
        StartMinimizedLabel = _localization["StartMinimizedLabel"];
        SystemTrayLabel = _localization["SystemTrayLabel"];
        MinimizeToTrayLabel = _localization["MinimizeToTrayLabel"];
        CloseToTrayLabel = _localization["CloseToTrayLabel"];
        WidgetLabel = _localization["WidgetLabel"];
        EnableFloatingWidgetLabel = _localization["EnableFloatingWidgetLabel"];
        AlwaysOnTopLabel = _localization["AlwaysOnTopLabel"];
        ShowWidgetLabel = _localization["ShowFloatingWidgetLabel"];
        HideWidgetLabel = _localization["HideFloatingWidgetLabel"];
        AlertsLabel = _localization["AlertsLabel"];
        CooldownLabel = _localization["CooldownLabel"];
        MinutesLabel = _localization["MinutesLabel"];
        CooldownRangeLabel = _localization["CooldownRangeLabel"];
        SaveLabel = _localization["SaveLabel"];
        ResetToDefaultsLabel = _localization["ResetToDefaultsLabel"];
        GetStartedLabel = _localization["GetStartedReopen"];

        foreach (var rule in AlertRules)
        {
            rule.Name = _localization[AlertTypeKey(GetAlertType(rule.Id))];
        }
    }

    private bool GetBool(string key, bool defaultValue)
    {
        var value = _settings.Get(key, string.Empty);
        return string.IsNullOrEmpty(value) ? defaultValue : bool.TryParse(value, out var parsed) && parsed;
    }

    private static bool IsSpeedRule(string id) => id is "highDownloadSpeed" or "highUploadSpeed";

    private static AlertType GetAlertType(string id) => id switch
    {
        "highDownloadSpeed" => AlertType.HighDownloadSpeed,
        "highUploadSpeed" => AlertType.HighUploadSpeed,
        "dailyDownloadLimit" => AlertType.DailyDownloadLimit,
        "dailyUploadLimit" => AlertType.DailyUploadLimit,
        "dailyTotalLimit" => AlertType.DailyTotalLimit,
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };

    private static string AlertTypeKey(AlertType type) => type switch
    {
        AlertType.HighDownloadSpeed => "AlertTypeHighDownloadSpeed",
        AlertType.HighUploadSpeed => "AlertTypeHighUploadSpeed",
        AlertType.DailyDownloadLimit => "AlertTypeDailyDownloadLimit",
        AlertType.DailyUploadLimit => "AlertTypeDailyUploadLimit",
        AlertType.DailyTotalLimit => "AlertTypeDailyTotalLimit",
        _ => "AlertTypeUnknown"
    };

    private AlertRuleViewModel RuleFor(string id) => AlertRules.First(r => r.Id == id);

    private string FormatThreshold(double bytes, bool isSpeed)
    {
        if (bytes <= 0)
        {
            return bytes.ToString(CultureInfo.InvariantCulture);
        }

        if (isSpeed)
        {
            var kib = bytes / 1024.0;
            var mib = bytes / (1024.0 * 1024.0);
            var gib = bytes / (1024.0 * 1024.0 * 1024.0);
            if (gib >= 1)
            {
                return RoundTrim(gib);
            }

            return mib >= 1 ? RoundTrim(mib) : RoundTrim(kib);
        }

        var mb = bytes / (1024.0 * 1024.0);
        var gb = bytes / (1024.0 * 1024.0 * 1024.0);
        var tb = bytes / (1024.0 * 1024.0 * 1024.0 * 1024.0);
        if (tb >= 1)
        {
            return RoundTrim(tb);
        }

        return gb >= 1 ? RoundTrim(gb) : RoundTrim(mb);
    }

    private static string RoundTrim(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private int GetUnitIndex(string id, double bytes)
    {
        if (bytes <= 0)
        {
            return GetDefaultUnitIndex(id);
        }

        if (IsSpeedRule(id))
        {
            var mib = bytes / (1024.0 * 1024.0);
            var gib = bytes / (1024.0 * 1024.0 * 1024.0);
            if (gib >= 1)
            {
                return 2;
            }

            return mib >= 1 ? 1 : 0;
        }

        var gb = bytes / (1024.0 * 1024.0 * 1024.0);
        var tb = bytes / (1024.0 * 1024.0 * 1024.0 * 1024.0);
        if (tb >= 1)
        {
            return 2;
        }

        var mb = bytes / (1024.0 * 1024.0);
        return gb >= 1 ? 1 : 0;
    }

    private static int GetDefaultUnitIndex(string id) => IsSpeedRule(id) ? 1 : 1;

    private double ThresholdBytes(string id) =>
        ConvertToBytes(ParseNumber(RuleFor(id).ThresholdText), IsSpeedRule(id) ? 1024.0 : 1024.0 * 1024.0, RuleFor(id).UnitIndex);

    private static double ConvertToBytes(double value, double baseMultiplier, int unitIndex) =>
        unitIndex switch
        {
            0 => value * baseMultiplier,
            1 => value * baseMultiplier * 1024.0,
            2 => value * baseMultiplier * 1024.0 * 1024.0,
            _ => value * baseMultiplier
        };

    private AlertConfig BuildAlertConfig()
    {
        return new AlertConfig(
            HighDownloadSpeedEnabled: RuleFor("highDownloadSpeed").IsEnabled,
            HighDownloadSpeedThresholdBytesPerSecond: ThresholdBytes("highDownloadSpeed"),
            HighUploadSpeedEnabled: RuleFor("highUploadSpeed").IsEnabled,
            HighUploadSpeedThresholdBytesPerSecond: ThresholdBytes("highUploadSpeed"),
            DailyDownloadLimitEnabled: RuleFor("dailyDownloadLimit").IsEnabled,
            DailyDownloadLimitBytes: ThresholdBytes("dailyDownloadLimit"),
            DailyUploadLimitEnabled: RuleFor("dailyUploadLimit").IsEnabled,
            DailyUploadLimitBytes: ThresholdBytes("dailyUploadLimit"),
            DailyTotalLimitEnabled: RuleFor("dailyTotalLimit").IsEnabled,
            DailyTotalLimitBytes: ThresholdBytes("dailyTotalLimit"),
            Cooldown: TimeSpan.FromMinutes(ParseNumber(CooldownText)));
    }

    private static double ParseNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
             !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)))
        {
            return double.NaN;
        }

        return value;
    }
}

public sealed record LanguageOption(string DisplayName, string Culture);