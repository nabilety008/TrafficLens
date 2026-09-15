using System.Globalization;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Alerts;

namespace TrafficLens.App.Services;

/// <summary>
/// Maps <see cref="AlertConfig"/> to and from the existing settings store
/// (no new settings file). All rules default to disabled at their suggested
/// thresholds. Also persists the minimal per-rule last-triggered local date so a
/// daily alert does not re-fire after a restart on the same local day.
/// </summary>
public static class AlertSettings
{
    private const string Prefix = "alerts.";

    public const string HighDownloadSpeedEnabledKey = Prefix + "highDownloadSpeed.enabled";
    public const string HighDownloadSpeedThresholdKey = Prefix + "highDownloadSpeed.threshold";
    public const string HighUploadSpeedEnabledKey = Prefix + "highUploadSpeed.enabled";
    public const string HighUploadSpeedThresholdKey = Prefix + "highUploadSpeed.threshold";
    public const string DailyDownloadLimitEnabledKey = Prefix + "dailyDownloadLimit.enabled";
    public const string DailyDownloadLimitThresholdKey = Prefix + "dailyDownloadLimit.threshold";
    public const string DailyUploadLimitEnabledKey = Prefix + "dailyUploadLimit.enabled";
    public const string DailyUploadLimitThresholdKey = Prefix + "dailyUploadLimit.threshold";
    public const string DailyTotalLimitEnabledKey = Prefix + "dailyTotalLimit.enabled";
    public const string DailyTotalLimitThresholdKey = Prefix + "dailyTotalLimit.threshold";
    public const string CooldownSecondsKey = Prefix + "cooldownSeconds";

    public const string LastTriggeredDailyDownloadKey = Prefix + "lastTriggered.dailyDownload";
    public const string LastTriggeredDailyUploadKey = Prefix + "lastTriggered.dailyUpload";
    public const string LastTriggeredDailyTotalKey = Prefix + "lastTriggered.dailyTotal";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static AlertConfig Load(ISettingsService settings)
    {
        var defaults = AlertConfig.Default();
        return new AlertConfig(
            HighDownloadSpeedEnabled: settings.GetBool(
                HighDownloadSpeedEnabledKey, defaults.HighDownloadSpeedEnabled),
            HighDownloadSpeedThresholdBytesPerSecond: settings.GetDouble(
                HighDownloadSpeedThresholdKey, defaults.HighDownloadSpeedThresholdBytesPerSecond),
            HighUploadSpeedEnabled: settings.GetBool(
                HighUploadSpeedEnabledKey, defaults.HighUploadSpeedEnabled),
            HighUploadSpeedThresholdBytesPerSecond: settings.GetDouble(
                HighUploadSpeedThresholdKey, defaults.HighUploadSpeedThresholdBytesPerSecond),
            DailyDownloadLimitEnabled: settings.GetBool(
                DailyDownloadLimitEnabledKey, defaults.DailyDownloadLimitEnabled),
            DailyDownloadLimitBytes: settings.GetDouble(
                DailyDownloadLimitThresholdKey, defaults.DailyDownloadLimitBytes),
            DailyUploadLimitEnabled: settings.GetBool(
                DailyUploadLimitEnabledKey, defaults.DailyUploadLimitEnabled),
            DailyUploadLimitBytes: settings.GetDouble(
                DailyUploadLimitThresholdKey, defaults.DailyUploadLimitBytes),
            DailyTotalLimitEnabled: settings.GetBool(
                DailyTotalLimitEnabledKey, defaults.DailyTotalLimitEnabled),
            DailyTotalLimitBytes: settings.GetDouble(
                DailyTotalLimitThresholdKey, defaults.DailyTotalLimitBytes),
            Cooldown: TimeSpan.FromSeconds(settings.GetDouble(
                CooldownSecondsKey, defaults.Cooldown.TotalSeconds)));
    }

    public static void Save(ISettingsService settings, AlertConfig config)
    {
        settings.Set(HighDownloadSpeedEnabledKey, config.HighDownloadSpeedEnabled.ToString());
        settings.Set(HighDownloadSpeedThresholdKey, Format(config.HighDownloadSpeedThresholdBytesPerSecond));
        settings.Set(HighUploadSpeedEnabledKey, config.HighUploadSpeedEnabled.ToString());
        settings.Set(HighUploadSpeedThresholdKey, Format(config.HighUploadSpeedThresholdBytesPerSecond));
        settings.Set(DailyDownloadLimitEnabledKey, config.DailyDownloadLimitEnabled.ToString());
        settings.Set(DailyDownloadLimitThresholdKey, Format(config.DailyDownloadLimitBytes));
        settings.Set(DailyUploadLimitEnabledKey, config.DailyUploadLimitEnabled.ToString());
        settings.Set(DailyUploadLimitThresholdKey, Format(config.DailyUploadLimitBytes));
        settings.Set(DailyTotalLimitEnabledKey, config.DailyTotalLimitEnabled.ToString());
        settings.Set(DailyTotalLimitThresholdKey, Format(config.DailyTotalLimitBytes));
        settings.Set(CooldownSecondsKey, Format(config.Cooldown.TotalSeconds));
        settings.Save();
    }

    public static IReadOnlyDictionary<AlertType, DateOnly?> LoadTriggeredDates(ISettingsService settings)
    {
        return new Dictionary<AlertType, DateOnly?>
        {
            [AlertType.DailyDownloadLimit] = ParseDate(settings.Get(LastTriggeredDailyDownloadKey, string.Empty)),
            [AlertType.DailyUploadLimit] = ParseDate(settings.Get(LastTriggeredDailyUploadKey, string.Empty)),
            [AlertType.DailyTotalLimit] = ParseDate(settings.Get(LastTriggeredDailyTotalKey, string.Empty))
        };
    }

    public static void SaveTriggeredDates(
        ISettingsService settings,
        IReadOnlyDictionary<AlertType, DateOnly?> dates)
    {
        settings.Set(LastTriggeredDailyDownloadKey, FormatDate(dates.GetValueOrDefault(AlertType.DailyDownloadLimit)));
        settings.Set(LastTriggeredDailyUploadKey, FormatDate(dates.GetValueOrDefault(AlertType.DailyUploadLimit)));
        settings.Set(LastTriggeredDailyTotalKey, FormatDate(dates.GetValueOrDefault(AlertType.DailyTotalLimit)));
        settings.Save();
    }

    private static string Format(double value) => value.ToString("0.##", Invariant);

    private static string FormatDate(DateOnly? date) => date?.ToString("yyyy-MM-dd", Invariant) ?? string.Empty;

    private static DateOnly? ParseDate(string text) =>
        DateOnly.TryParse(text, Invariant, DateTimeStyles.None, out var date) ? date : null;

    private static bool GetBool(this ISettingsService settings, string key, bool defaultValue) =>
        bool.TryParse(settings.Get(key, string.Empty), out var value) ? value : defaultValue;

    private static double GetDouble(this ISettingsService settings, string key, double defaultValue) =>
        double.TryParse(settings.Get(key, string.Empty), NumberStyles.Float, Invariant, out var value) ? value : defaultValue;
}