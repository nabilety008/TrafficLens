namespace TrafficLens.Core.Alerts;

/// <summary>
/// The five alert rules TrafficLens evaluates. Speed rules watch live aggregate
/// rate; usage rules watch the local-day totals produced by the history pipeline.
/// The set is intentionally fixed (no user-defined rules in this milestone).
/// </summary>
public enum AlertType
{
    HighDownloadSpeed,
    HighUploadSpeed,
    DailyDownloadLimit,
    DailyUploadLimit,
    DailyTotalLimit
}

public static class AlertTypeExtensions
{
    public static bool IsSpeedRule(this AlertType type) =>
        type is AlertType.HighDownloadSpeed or AlertType.HighUploadSpeed;

    public static bool IsDailyUsageRule(this AlertType type) =>
        type is AlertType.DailyDownloadLimit or AlertType.DailyUploadLimit or AlertType.DailyTotalLimit;
}