namespace TrafficLens.Core.Alerts;

/// <summary>
/// Alert configuration for all five rules. Speed thresholds are in bytes per
/// second; usage thresholds are in bytes per local calendar day. All rules
/// default to disabled at their suggested thresholds.
/// </summary>
public sealed record AlertConfig(
    bool HighDownloadSpeedEnabled,
    double HighDownloadSpeedThresholdBytesPerSecond,
    bool HighUploadSpeedEnabled,
    double HighUploadSpeedThresholdBytesPerSecond,
    bool DailyDownloadLimitEnabled,
    double DailyDownloadLimitBytes,
    bool DailyUploadLimitEnabled,
    double DailyUploadLimitBytes,
    bool DailyTotalLimitEnabled,
    double DailyTotalLimitBytes,
    TimeSpan Cooldown)
{
    public const long SuggestedHighDownloadSpeedThreshold = 50L * 1024 * 1024;
    public const long SuggestedHighUploadSpeedThreshold = 20L * 1024 * 1024;
    public const long SuggestedDailyDownloadLimit = 50L * 1024 * 1024 * 1024;
    public const long SuggestedDailyUploadLimit = 20L * 1024 * 1024 * 1024;
    public const long SuggestedDailyTotalLimit = 100L * 1024 * 1024 * 1024;

    public static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(5);

    public static AlertConfig Default() => new(
        HighDownloadSpeedEnabled: false,
        HighDownloadSpeedThresholdBytesPerSecond: SuggestedHighDownloadSpeedThreshold,
        HighUploadSpeedEnabled: false,
        HighUploadSpeedThresholdBytesPerSecond: SuggestedHighUploadSpeedThreshold,
        DailyDownloadLimitEnabled: false,
        DailyDownloadLimitBytes: SuggestedDailyDownloadLimit,
        DailyUploadLimitEnabled: false,
        DailyUploadLimitBytes: SuggestedDailyUploadLimit,
        DailyTotalLimitEnabled: false,
        DailyTotalLimitBytes: SuggestedDailyTotalLimit,
        Cooldown: DefaultCooldown);

    public bool IsRuleEnabled(AlertType type) => type switch
    {
        AlertType.HighDownloadSpeed => HighDownloadSpeedEnabled,
        AlertType.HighUploadSpeed => HighUploadSpeedEnabled,
        AlertType.DailyDownloadLimit => DailyDownloadLimitEnabled,
        AlertType.DailyUploadLimit => DailyUploadLimitEnabled,
        AlertType.DailyTotalLimit => DailyTotalLimitEnabled,
        _ => false
    };

    public double ThresholdOf(AlertType type) => type switch
    {
        AlertType.HighDownloadSpeed => HighDownloadSpeedThresholdBytesPerSecond,
        AlertType.HighUploadSpeed => HighUploadSpeedThresholdBytesPerSecond,
        AlertType.DailyDownloadLimit => DailyDownloadLimitBytes,
        AlertType.DailyUploadLimit => DailyUploadLimitBytes,
        AlertType.DailyTotalLimit => DailyTotalLimitBytes,
        _ => 0
    };
}