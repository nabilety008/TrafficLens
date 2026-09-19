namespace TrafficLens.Core.History;

/// <summary>
/// Pure builder for the Today hourly series. Slots are UTC-hour intervals
/// counted from the UTC instant corresponding to the local midnight of "today"
/// (the same instant the daily rollup uses), so a slot can never be ambiguous:
/// a 23-hour DST day yields 23 slots with a skipped local label, and a 25-hour
/// day yields 25 slots where two consecutive slots share one local label. The
/// final slot is the still-open current hour, clamped to "now". No OS calls,
/// fully unit-testable with hand-built <see cref="TimeZoneInfo"/> instances.
/// </summary>
public static class HourlyHistoryBuilder
{
    public static IReadOnlyList<HourlyUsagePoint> Build(
        DateTime nowUtc,
        TimeZoneInfo timeZone,
        IReadOnlyList<TrafficHistoryBucket> buckets)
    {
        var now = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        var today = HistoryRangeCalculator.LocalDateOf(now, timeZone);

        var dayStartUtc = DateTime.SpecifyKind(MidnightUtc(today, timeZone), DateTimeKind.Utc);
        if (now <= dayStartUtc)
        {
            return Array.Empty<HourlyUsagePoint>();
        }

        var slotCount = (int)Math.Round((MidnightUtc(today.AddDays(1), timeZone) - dayStartUtc).TotalHours);
        if (slotCount <= 0)
        {
            return Array.Empty<HourlyUsagePoint>();
        }

        var usage = new TrafficUsage[slotCount];
        foreach (var bucket in buckets)
        {
            var index = (int)Math.Floor((bucket.BucketStartUtc - dayStartUtc).TotalHours);
            if (index >= 0 && index < slotCount)
            {
                usage[index] += new TrafficUsage(bucket.DownloadBytes, bucket.UploadBytes);
            }
        }

        var nowTicks = now.Ticks;
        var points = new List<HourlyUsagePoint>();
        for (var i = 0; i < slotCount; i++)
        {
            var start = dayStartUtc.AddHours(i);
            if (start.Ticks >= nowTicks)
            {
                break;
            }

            var end = start.AddHours(1);
            var endTicks = end.Ticks;
            if (nowTicks < endTicks)
            {
                endTicks = nowTicks;
            }

            points.Add(new HourlyUsagePoint(
                start,
                new DateTime(endTicks, DateTimeKind.Utc),
                LocalHourOf(start, timeZone),
                usage[i].DownloadBytes,
                usage[i].UploadBytes));
        }

        return points;
    }

    public static DateTime MidnightUtc(DateOnly localDate, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(localDate.Year, localDate.Month, localDate.Day, 0, 0, 0, DateTimeKind.Unspecified),
            timeZone);

    private static int LocalHourOf(DateTime utc, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone).Hour;
}