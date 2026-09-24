using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using TrafficLens.App.Services;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Alerts;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.Localization;

namespace TrafficLens.WinUI.ViewModels;

public sealed class AlertsViewModel : INotifyPropertyChanged, IDisposable
{
    private const int MinCooldownMinutes = 1;
    private const int MaxCooldownMinutes = 1440;

    private readonly IAlertService _alertService;
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _localization;
    private readonly DispatcherQueue _dispatcherQueue;

    private readonly ObservableCollection<AlertRuleRowViewModel> _rules = new();
    private readonly ObservableCollection<AlertRecentRowViewModel> _recentAlerts = new();

    private bool _isActive;
    private bool _disposed;

    private string _alertsTitleLabel = string.Empty;
    private string _configuredRulesLabel = string.Empty;
    private string _noConfiguredRulesText = string.Empty;
    private string _triggeredSectionLabel = string.Empty;
    private string _noAlertsText = string.Empty;
    private string _alertCountTemplate = string.Empty;
    private string _countText = string.Empty;
    private string _cooldownLabel = string.Empty;
    private string _minutesLabel = string.Empty;
    private string _cooldownRangeLabel = string.Empty;
    private string _cooldownText = "5";
    private string _validationError = string.Empty;
    private string _savedNotice = string.Empty;
    private string _saveLabel = string.Empty;
    private string _enabledLabel = string.Empty;
    private string _disabledLabel = string.Empty;
    private bool _hasValidationError;
    private bool _hasSavedNotice;

    public AlertsViewModel(
        IAlertService alertService,
        ISettingsService settings,
        ILocalizationService localization,
        DispatcherQueue dispatcherQueue)
    {
        _alertService = alertService;
        _settings = settings;
        _localization = localization;
        _dispatcherQueue = dispatcherQueue;

        _alertService.AlertRaised += OnAlertRaised;
        _alertService.ConfigChanged += OnConfigChanged;
        _localization.CultureChanged += OnCultureChanged;

        RebuildRules();
        RebuildRecent();
        _cooldownText = ((int)_alertService.CurrentConfig.Cooldown.TotalMinutes)
            .ToString(CultureInfo.InvariantCulture);

        RefreshLocalizedStrings();
        OnPropertyChanged(nameof(CooldownText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<AlertRuleRowViewModel> Rules => _rules;

    public ObservableCollection<AlertRecentRowViewModel> RecentAlerts => _recentAlerts;

    public string AlertsTitleLabel
    {
        get => _alertsTitleLabel;
        private set => SetProperty(ref _alertsTitleLabel, value);
    }

    public string ConfiguredRulesLabel
    {
        get => _configuredRulesLabel;
        private set => SetProperty(ref _configuredRulesLabel, value);
    }

    public string NoConfiguredRulesText
    {
        get => _noConfiguredRulesText;
        private set => SetProperty(ref _noConfiguredRulesText, value);
    }

    public string TriggeredSectionLabel
    {
        get => _triggeredSectionLabel;
        private set => SetProperty(ref _triggeredSectionLabel, value);
    }

    public string NoAlertsText
    {
        get => _noAlertsText;
        private set => SetProperty(ref _noAlertsText, value);
    }

    public string AlertCountText
    {
        get => _countText;
        private set => SetProperty(ref _countText, value);
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

    public string CooldownText
    {
        get => _cooldownText;
        set => SetProperty(ref _cooldownText, value ?? string.Empty);
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

    public bool HasValidationError
    {
        get => _hasValidationError;
        private set => SetProperty(ref _hasValidationError, value);
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

    public bool HasSavedNotice
    {
        get => _hasSavedNotice;
        private set => SetProperty(ref _hasSavedNotice, value);
    }

    public string SaveLabel
    {
        get => _saveLabel;
        private set => SetProperty(ref _saveLabel, value);
    }

    public string EnabledLabel
    {
        get => _enabledLabel;
        private set => SetProperty(ref _enabledLabel, value);
    }

    public string DisabledLabel
    {
        get => _disabledLabel;
        private set => SetProperty(ref _disabledLabel, value);
    }

    public bool HasNoConfiguredRules => !_rules.Any(r => r.IsEnabled);

    public bool HasNoAlerts => _recentAlerts.Count == 0;

    public void SetActive(bool active)
    {
        _isActive = active;
        if (active)
        {
            RebuildRecent();
            RebuildRules();
        }
    }

    public void Save()
    {
        SaveDraft(showSaved: true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _alertService.AlertRaised -= OnAlertRaised;
        _alertService.ConfigChanged -= OnConfigChanged;
        _localization.CultureChanged -= OnCultureChanged;
    }

    private void OnAlertRaised(object? sender, AlertRaisedEventArgs e) =>
        RunOnUi(() =>
        {
            if (!_isActive)
            {
                return;
            }

            _recentAlerts.Insert(0, new AlertRecentRowViewModel(_localization, e.Alert));
            OnPropertyChanged(nameof(HasNoAlerts));
            AlertCountText = FormatCount();
        });

    private void OnConfigChanged(object? sender, EventArgs e) =>
        RunOnUi(() =>
        {
            if (!_isActive)
            {
                return;
            }

            RebuildRules();
            _cooldownText = ((int)_alertService.CurrentConfig.Cooldown.TotalMinutes)
                .ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(CooldownText));
        });

    private void OnCultureChanged(object? sender, EventArgs e) =>
        RunOnUi(() =>
        {
            RefreshLocalizedStrings();
            foreach (var rule in _rules)
            {
                rule.RefreshLocalized(_localization);
            }

            RebuildRecent();
        });

    private void RunOnUi(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcherQueue.TryEnqueue(() => action());
    }

    private void RebuildRules()
    {
        var config = _alertService.CurrentConfig;
        _rules.Clear();

        foreach (var type in Enum.GetValues<AlertType>())
        {
            var row = new AlertRuleRowViewModel(_localization, type, config, OnRuleEdited);
            _rules.Add(row);
        }

        OnPropertyChanged(nameof(HasNoConfiguredRules));
    }

    private void RebuildRecent()
    {
        _recentAlerts.Clear();
        foreach (var alert in _alertService.RecentAlerts)
        {
            _recentAlerts.Add(new AlertRecentRowViewModel(_localization, alert));
        }

        OnPropertyChanged(nameof(HasNoAlerts));
        AlertCountText = FormatCount();
    }

    private void OnRuleEdited()
    {
        OnPropertyChanged(nameof(HasNoConfiguredRules));
    }

    private void SaveDraft(bool showSaved = false)
    {
        ValidationError = string.Empty;
        HasValidationError = false;
        SavedNotice = string.Empty;
        HasSavedNotice = false;

        var cooldown = ParseNumber(CooldownText);
        if (double.IsNaN(cooldown) || cooldown < MinCooldownMinutes || cooldown > MaxCooldownMinutes)
        {
            ValidationError = _localization["InvalidValueLabel"];
            HasValidationError = true;
            return;
        }

        foreach (var rule in _rules)
        {
            var value = ParseNumber(rule.ThresholdText);
            if (double.IsNaN(value) || value <= 0)
            {
                ValidationError = _localization["InvalidValueLabel"];
                HasValidationError = true;
                return;
            }
        }

        var newConfig = BuildAlertConfig(cooldown);
        var current = AlertSettings.Load(_settings);
        if (newConfig != current)
        {
            AlertSettings.Save(_settings, newConfig);
            _alertService.RefreshConfig();
        }

        if (showSaved)
        {
            SavedNotice = _localization["ChangesSavedLabel"];
            HasSavedNotice = true;
        }
    }

    private AlertConfig BuildAlertConfig(double cooldownMinutes)
    {
        double Threshold(AlertRuleRowViewModel rule)
        {
            var value = ParseNumber(rule.ThresholdText);
            var isSpeed = rule.Type.IsSpeedRule();
            var baseMultiplier = isSpeed ? 1024.0 : 1024.0 * 1024.0;
            return rule.UnitIndex switch
            {
                0 => value * baseMultiplier,
                1 => value * baseMultiplier * 1024.0,
                2 => value * baseMultiplier * 1024.0 * 1024.0,
                _ => value * baseMultiplier
            };
        }

        AlertRuleRowViewModel Row(AlertType type) => _rules.First(r => r.Type == type);

        return new AlertConfig(
            HighDownloadSpeedEnabled: Row(AlertType.HighDownloadSpeed).IsEnabled,
            HighDownloadSpeedThresholdBytesPerSecond: Threshold(Row(AlertType.HighDownloadSpeed)),
            HighUploadSpeedEnabled: Row(AlertType.HighUploadSpeed).IsEnabled,
            HighUploadSpeedThresholdBytesPerSecond: Threshold(Row(AlertType.HighUploadSpeed)),
            DailyDownloadLimitEnabled: Row(AlertType.DailyDownloadLimit).IsEnabled,
            DailyDownloadLimitBytes: Threshold(Row(AlertType.DailyDownloadLimit)),
            DailyUploadLimitEnabled: Row(AlertType.DailyUploadLimit).IsEnabled,
            DailyUploadLimitBytes: Threshold(Row(AlertType.DailyUploadLimit)),
            DailyTotalLimitEnabled: Row(AlertType.DailyTotalLimit).IsEnabled,
            DailyTotalLimitBytes: Threshold(Row(AlertType.DailyTotalLimit)),
            Cooldown: TimeSpan.FromMinutes(cooldownMinutes));
    }

    private string FormatCount()
    {
        var count = _recentAlerts.Count;
        return count == 0
            ? string.Empty
            : string.Format(_localization.CurrentCulture, _alertCountTemplate, count);
    }

    private void RefreshLocalizedStrings()
    {
        AlertsTitleLabel = _localization["AlertsTitleLabel"];
        ConfiguredRulesLabel = _localization["AlertsConfiguredRulesLabel"];
        NoConfiguredRulesText = _localization["AlertsNoConfiguredRulesLabel"];
        TriggeredSectionLabel = _localization["AlertsTriggeredSectionLabel"];
        NoAlertsText = _localization["AlertsNoTriggeredLabel"];
        _alertCountTemplate = _localization["AlertsCountFormat"];
        CooldownLabel = _localization["CooldownLabel"];
        MinutesLabel = _localization["MinutesLabel"];
        CooldownRangeLabel = _localization["CooldownRangeLabel"];
        SaveLabel = _localization["SaveLabel"];
        EnabledLabel = _localization["AlertStatusEnabledLabel"];
        DisabledLabel = _localization["AlertStatusDisabledLabel"];
        AlertCountText = FormatCount();
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public sealed class AlertRuleRowViewModel : INotifyPropertyChanged
{
    private bool _isEnabled;
    private string _thresholdText = string.Empty;
    private int _unitIndex;
    private readonly Action _onEdited;

    public AlertRuleRowViewModel(
        ILocalizationService localization,
        AlertType type,
        AlertConfig config,
        Action onEdited)
    {
        Type = type;
        _onEdited = onEdited;
        RefreshLocalized(localization);

        _isEnabled = config.IsRuleEnabled(type);
        _thresholdText = FormatThreshold(config.ThresholdOf(type), type.IsSpeedRule());
        _unitIndex = GetUnitIndex(type, config.ThresholdOf(type));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AlertType Type { get; }

    public string[] ActiveUnitOptions => Type.IsSpeedRule()
        ? new[] { "KB/s", "MB/s", "GB/s" }
        : new[] { "MB", "GB", "TB" };

    public string RuleName { get; private set; } = string.Empty;

    public string StatusText => IsEnabled ? _enabledLabel : _disabledLabel;

    private string _enabledLabel = string.Empty;
    private string _disabledLabel = string.Empty;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
            _onEdited();
        }
    }

    public string ThresholdText
    {
        get => _thresholdText;
        set
        {
            if (_thresholdText == value)
            {
                return;
            }

            _thresholdText = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThresholdText)));
            _onEdited();
        }
    }

    public int UnitIndex
    {
        get => _unitIndex;
        set
        {
            if (_unitIndex == value)
            {
                return;
            }

            _unitIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnitIndex)));
            _onEdited();
        }
    }

    public void RefreshLocalized(ILocalizationService localization)
    {
        RuleName = localization[AlertTypeKey(Type)];
        _enabledLabel = localization["AlertStatusEnabledLabel"];
        _disabledLabel = localization["AlertStatusDisabledLabel"];
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RuleName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
    }

    private static string AlertTypeKey(AlertType type) => type switch
    {
        AlertType.HighDownloadSpeed => "AlertTypeHighDownloadSpeed",
        AlertType.HighUploadSpeed => "AlertTypeHighUploadSpeed",
        AlertType.DailyDownloadLimit => "AlertTypeDailyDownloadLimit",
        AlertType.DailyUploadLimit => "AlertTypeDailyUploadLimit",
        AlertType.DailyTotalLimit => "AlertTypeDailyTotalLimit",
        _ => "AlertTypeUnknown"
    };

    private static string FormatThreshold(double bytes, bool isSpeed)
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

    private static int GetUnitIndex(AlertType type, double bytes)
    {
        if (bytes <= 0)
        {
            return 1;
        }

        if (type.IsSpeedRule())
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

        return gb >= 1 ? 1 : 0;
    }
}

public sealed class AlertRecentRowViewModel
{
    private readonly ILocalizationService _localization;
    private readonly AlertEvent _alert;

    public AlertRecentRowViewModel(ILocalizationService localization, AlertEvent alert)
    {
        _localization = localization;
        _alert = alert;
        Message = AlertMessageFormatter.Message(localization, alert);
        TimeText = alert.OccurredUtc.ToLocalTime().ToString("HH:mm:ss");
        TypeText = localization[AlertTypeKey(alert.Type)];
        ThresholdText = string.Format(
            localization.CurrentCulture,
            localization["AlertThresholdFormat"],
            alert.Type.IsSpeedRule()
                ? DataRateFormatter.FormatAdaptive((long)Math.Max(0, Math.Round(alert.Threshold)), localization.CurrentCulture)
                : DataSizeFormatter.Format((long)Math.Max(0, Math.Round(alert.Threshold)), localization.CurrentCulture));
    }

    public string Message { get; }

    public string TimeText { get; }

    public string TypeText { get; }

    public string ThresholdText { get; }

    private static string AlertTypeKey(AlertType type) => type switch
    {
        AlertType.HighDownloadSpeed => "AlertTypeHighDownloadSpeed",
        AlertType.HighUploadSpeed => "AlertTypeHighUploadSpeed",
        AlertType.DailyDownloadLimit => "AlertTypeDailyDownloadLimit",
        AlertType.DailyUploadLimit => "AlertTypeDailyUploadLimit",
        AlertType.DailyTotalLimit => "AlertTypeDailyTotalLimit",
        _ => "AlertTypeUnknown"
    };
}
