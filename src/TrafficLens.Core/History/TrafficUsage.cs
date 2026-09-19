namespace TrafficLens.Core.History;

/// <summary>
/// A byte total observed by TrafficLens over some range. Values are DERIVED FROM
/// VALID COUNTER DELTAS (never from integrating displayed speed samples) and are
/// technical values, so they are rendered LTR and untranslated.
/// </summary>
public readonly record struct TrafficUsage(long DownloadBytes, long UploadBytes)
{
    public long TotalBytes => DownloadBytes + UploadBytes;

    public static TrafficUsage Empty => default;

    public static TrafficUsage operator +(TrafficUsage left, TrafficUsage right) =>
        new(left.DownloadBytes + right.DownloadBytes, left.UploadBytes + right.UploadBytes);
}

/// <summary>
/// One day of persisted usage, keyed by the user's local calendar date.
/// </summary>
public readonly record struct DailyUsagePoint(DateOnly Date, long DownloadBytes, long UploadBytes)
{
    public long TotalBytes => DownloadBytes + UploadBytes;
}

/// <summary>
/// An immutable, ready-to-render view of the persisted history. Produced off the
/// UI thread by the history service so the ViewModel never runs SQL.
/// </summary>
public sealed record HistorySnapshot(
    bool IsAvailable,
    string? Error,
    TrafficUsage Today,
    TrafficUsage Yesterday,
    TrafficUsage Last7Days,
    TrafficUsage Last30Days,
    TrafficUsage Lifetime,
    IReadOnlyList<DailyUsagePoint> DailySeries,
    IReadOnlyList<HourlyUsagePoint> TodayHourly)
{
    public static HistorySnapshot Unavailable(string? error) => new(
        false,
        error,
        TrafficUsage.Empty,
        TrafficUsage.Empty,
        TrafficUsage.Empty,
        TrafficUsage.Empty,
        TrafficUsage.Empty,
        Array.Empty<DailyUsagePoint>(),
        Array.Empty<HourlyUsagePoint>());

    public TrafficUsage For(HistoryRange range) => range switch
    {
        HistoryRange.Today => Today,
        HistoryRange.Yesterday => Yesterday,
        HistoryRange.Last7Days => Last7Days,
        HistoryRange.Last30Days => Last30Days,
        HistoryRange.Lifetime => Lifetime,
        _ => TrafficUsage.Empty
    };
}
