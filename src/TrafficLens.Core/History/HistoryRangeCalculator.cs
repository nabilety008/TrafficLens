namespace TrafficLens.Core.History;

/// <summary>
/// Pure helpers for translating reporting ranges into LOCAL calendar dates and
/// UTC instants into local dates. Local day boundaries therefore follow the
/// user's real calendar (midnight rollover and DST included) rather than an
/// assumed 24-hour/UTC day. No OS calls, fully unit-testable.
/// </summary>
public static class HistoryRangeCalculator
{
    public const int Last7DayCount = 7;
    public const int Last30DayCount = 30;

    /// <summary>Converts a UTC instant to the local calendar date in <paramref name="timeZone"/>.</summary>
    public static DateOnly LocalDateOf(DateTime utc, TimeZoneInfo timeZone)
    {
        var utcKind = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcKind, timeZone);
        return DateOnly.FromDateTime(local);
    }

    /// <summary>
    /// The half-open LOCAL date range [Start, EndExclusive) for a range, or null
    /// for <see cref="HistoryRange.Lifetime"/> (which has no lower bound). "Last 7
    /// days" and "Last 30 days" are inclusive of today (today plus the preceding
    /// 6/29 local days).
    /// </summary>
    public static (DateOnly Start, DateOnly EndExclusive)? ToLocalDateRange(
        HistoryRange range,
        DateOnly today) => range switch
    {
        HistoryRange.Today => (today, today.AddDays(1)),
        HistoryRange.Yesterday => (today.AddDays(-1), today),
        HistoryRange.Last7Days => (today.AddDays(-(Last7DayCount - 1)), today.AddDays(1)),
        HistoryRange.Last30Days => (today.AddDays(-(Last30DayCount - 1)), today.AddDays(1)),
        _ => null
    };

    /// <summary>
    /// Builds the contiguous local-day series (oldest first) spanning the given
    /// half-open range, looking each day up in <paramref name="byDate"/>. Days
    /// without data are represented as zero, never fabricated with a value.
    /// </summary>
    public static IReadOnlyList<DailyUsagePoint> BuildDailySeries(
        DateOnly startInclusive,
        DateOnly endExclusive,
        IReadOnlyDictionary<DateOnly, TrafficUsage> byDate)
    {
        var points = new List<DailyUsagePoint>();
        for (var date = startInclusive; date < endExclusive; date = date.AddDays(1))
        {
            var usage = byDate.TryGetValue(date, out var value) ? value : TrafficUsage.Empty;
            points.Add(new DailyUsagePoint(date, usage.DownloadBytes, usage.UploadBytes));
        }

        return points;
    }

    public static TrafficUsage SumDaily(
        IReadOnlyDictionary<DateOnly, TrafficUsage> byDate,
        DateOnly startInclusive,
        DateOnly endExclusive)
    {
        var total = TrafficUsage.Empty;
        for (var date = startInclusive; date < endExclusive; date = date.AddDays(1))
        {
            if (byDate.TryGetValue(date, out var value))
            {
                total += value;
            }
        }

        return total;
    }
}
