using TrafficLens.Core.History;

namespace TrafficLens.Infrastructure.Tests;

public class HourlyHistoryBuilderTests
{
    private static DateTime Utc(int y, int m, int d, int h = 0, int min = 0) =>
        new(y, m, d, h, min, 0, DateTimeKind.Utc);

    private static TrafficHistoryBucket Bucket(DateTime startUtc, long download, long upload) =>
        new(startUtc, 60, download, upload);

    private static TimeZoneInfo FixedZone(TimeSpan offset) =>
        TimeZoneInfo.CreateCustomTimeZone($"Fixed{offset.Hours}", offset, "Fixed", "Fixed");

    private static TimeZoneInfo EuDstZone() =>
        TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");

    [Fact]
    public void Build_EmptyBuckets_ReturnsZeroPaddedSlotsUpToNow()
    {
        var series = HourlyHistoryBuilder.Build(Utc(2026, 6, 15, 10, 45), TimeZoneInfo.Utc, Array.Empty<TrafficHistoryBucket>());

        Assert.Equal(11, series.Count);
        Assert.Equal(Utc(2026, 6, 15, 10, 0), series[^1].StartUtc);
        Assert.Equal(Utc(2026, 6, 15, 10, 45), series[^1].EndUtcExclusive);
        Assert.Equal(10, series[^1].LocalHour);
        Assert.All(series, p => Assert.Equal(0, p.TotalBytes));
    }

    [Fact]
    public void Build_SumsBucketsIntoCorrectSlots_ClampsPartialHour()
    {
        var buckets = new[]
        {
            Bucket(Utc(2026, 6, 15, 0, 30), 100, 0),
            Bucket(Utc(2026, 6, 15, 1, 0), 50, 20),
            Bucket(Utc(2026, 6, 15, 10, 30), 7, 8)
        };

        var series = HourlyHistoryBuilder.Build(Utc(2026, 6, 15, 10, 45), TimeZoneInfo.Utc, buckets);

        Assert.Equal(11, series.Count);
        Assert.Equal(100, series[0].DownloadBytes);
        Assert.Equal(50, series[1].DownloadBytes);
        Assert.Equal(20, series[1].UploadBytes);
        Assert.Equal(0, series[2].TotalBytes);
        Assert.Equal(7, series[10].DownloadBytes);
        Assert.Equal(8, series[10].UploadBytes);
        Assert.Equal(Utc(2026, 6, 15, 10, 45), series[10].EndUtcExclusive);
        Assert.Equal(0, series[0].LocalHour);
        Assert.Equal(10, series[10].LocalHour);
        Assert.Equal(185, series.Sum(p => p.TotalBytes));
    }

    [Fact]
    public void Build_TopOfHour_ExcludesJustStartedHourWithNoData()
    {
        var series = HourlyHistoryBuilder.Build(Utc(2026, 6, 15, 10, 0), TimeZoneInfo.Utc, Array.Empty<TrafficHistoryBucket>());
        Assert.Equal(10, series.Count);
    }

    [Fact]
    public void Build_AtLocalMidnight_ReturnsEmptySeries()
    {
        var series = HourlyHistoryBuilder.Build(Utc(2026, 6, 15, 0, 0), TimeZoneInfo.Utc, Array.Empty<TrafficHistoryBucket>());
        Assert.Empty(series);
    }

    [Fact]
    public void Build_IgnoresBucketsOutsideTheDayWindow()
    {
        var buckets = new[]
        {
            Bucket(Utc(2026, 6, 14, 23, 0), 999, 0),
            Bucket(Utc(2026, 6, 15, 0, 30), 100, 0),
            Bucket(Utc(2026, 6, 15, 23, 30), 999, 0)
        };

        var series = HourlyHistoryBuilder.Build(Utc(2026, 6, 15, 10, 45), TimeZoneInfo.Utc, buckets);

        Assert.Equal(100, series.Sum(p => p.TotalBytes));
        Assert.Equal(100, series[0].DownloadBytes);
    }

    [Fact]
    public void Build_SpringForwardDay_Has23SlotsAndSkipsGapHourLabel()
    {
        var series = HourlyHistoryBuilder.Build(Utc(2026, 3, 29, 15, 0), EuDstZone(), Array.Empty<TrafficHistoryBucket>());

        Assert.Equal(16, series.Count);
        Assert.DoesNotContain(series, p => p.LocalHour == 2);
        Assert.Equal(3, series[2].LocalHour);
    }

    [Fact]
    public void Build_FallBackDay_Has25SlotsWithDuplicateLocalHour()
    {
        var series = HourlyHistoryBuilder.Build(Utc(2026, 10, 25, 14, 0), EuDstZone(), Array.Empty<TrafficHistoryBucket>());

        Assert.Equal(16, series.Count);
        var duplicated = series.Where(p => p.LocalHour == 2).Select(p => p.StartUtc).ToArray();
        Assert.Equal(2, duplicated.Length);
        Assert.Equal(Utc(2026, 10, 25, 0, 0), duplicated[0]);
        Assert.Equal(Utc(2026, 10, 25, 1, 0), duplicated[1]);
        Assert.Equal(Utc(2026, 10, 25, 14, 0), series[^1].EndUtcExclusive);
    }

    [Fact]
    public void Build_HalfHourOffsetZone_UsesUnalignedUtcSlots()
    {
        var zone = FixedZone(TimeSpan.FromHours(5.5));
        var series = HourlyHistoryBuilder.Build(Utc(2026, 1, 15, 6, 15), zone, Array.Empty<TrafficHistoryBucket>());

        Assert.Equal(12, series.Count);
        Assert.Equal(Utc(2026, 1, 14, 18, 30), series[0].StartUtc);
        Assert.Equal(0, series[0].LocalHour);
        Assert.Equal(11, series[^1].LocalHour);
        Assert.Equal(Utc(2026, 1, 15, 6, 15), series[^1].EndUtcExclusive);
    }
}