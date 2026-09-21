using TrafficLens.Core.History;

namespace TrafficLens.Infrastructure.Tests;

public class HistoryRangeCalculatorTests
{
    private static readonly TimeZoneInfo TestZone = TimeZoneInfo.Local;

    [Fact]
    public void LocalDateOf_ReturnsLocalDate()
    {
        var utc = new DateTime(2026, 1, 15, 2, 30, 0, DateTimeKind.Utc);
        var expected = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, TestZone));
        Assert.Equal(expected, HistoryRangeCalculator.LocalDateOf(utc, TestZone));
    }

    [Theory]
    [InlineData(HistoryRange.Today, 0)]
    [InlineData(HistoryRange.Yesterday, -1)]
    [InlineData(HistoryRange.Last7Days, -6)]
    [InlineData(HistoryRange.Last30Days, -29)]
    public void ToLocalDateRange_StartIsCorrect(HistoryRange range, int expectedStartOffset)
    {
        var today = new DateOnly(2026, 1, 5);
        var result = HistoryRangeCalculator.ToLocalDateRange(range, today);
        Assert.NotNull(result);
        Assert.Equal(today.AddDays(expectedStartOffset), result.Value.Start);
    }

    [Fact]
    public void ToLocalDateRange_LifetimeReturnsNull()
    {
        Assert.Null(HistoryRangeCalculator.ToLocalDateRange(HistoryRange.Lifetime, new DateOnly(2026, 1, 5)));
    }

    [Fact]
    public void ToLocalDateRange_ThisMonth_StartsAtFirstDayOfMonth()
    {
        var today = new DateOnly(2026, 3, 15);
        var result = HistoryRangeCalculator.ToLocalDateRange(HistoryRange.ThisMonth, today);
        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 3, 1), result.Value.Start);
        Assert.Equal(today.AddDays(1), result.Value.EndExclusive);
    }

    [Fact]
    public void ToLocalDateRange_ThisMonth_FirstDayOfMonth()
    {
        var today = new DateOnly(2026, 1, 1);
        var result = HistoryRangeCalculator.ToLocalDateRange(HistoryRange.ThisMonth, today);
        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 1, 1), result.Value.Start);
        Assert.Equal(today.AddDays(1), result.Value.EndExclusive);
    }

    [Fact]
    public void ToLocalDateRange_ThisMonth_EndOfYear()
    {
        var today = new DateOnly(2026, 12, 31);
        var result = HistoryRangeCalculator.ToLocalDateRange(HistoryRange.ThisMonth, today);
        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 12, 1), result.Value.Start);
        Assert.Equal(today.AddDays(1), result.Value.EndExclusive);
    }

    [Fact]
    public void ToLocalDateRange_ThisMonth_NotRolling30Days()
    {
        var today = new DateOnly(2026, 2, 15);
        var result = HistoryRangeCalculator.ToLocalDateRange(HistoryRange.ThisMonth, today);
        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 2, 1), result.Value.Start);
        Assert.NotEqual(today.AddDays(-29), result.Value.Start);
    }

    [Fact]
    public void BuildDailySeries_ReturnsContiguousZeroPaddedDays()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 4);
        var byDate = new Dictionary<DateOnly, TrafficUsage>
        {
            [new DateOnly(2026, 1, 2)] = new TrafficUsage(100, 200)
        };
        var series = HistoryRangeCalculator.BuildDailySeries(start, end, byDate);
        Assert.Equal(3, series.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), series[0].Date);
        Assert.Equal(0, series[0].TotalBytes);
        Assert.Equal(300, series[1].TotalBytes);
        Assert.Equal(0, series[2].TotalBytes);
    }

    [Fact]
    public void SumDaily_SumsRangeCorrectly()
    {
        var dict = new Dictionary<DateOnly, TrafficUsage>
        {
            [new DateOnly(2026, 1, 1)] = new TrafficUsage(10, 0),
            [new DateOnly(2026, 1, 2)] = new TrafficUsage(0, 20),
            [new DateOnly(2026, 1, 3)] = new TrafficUsage(5, 5)
        };
        var sum = HistoryRangeCalculator.SumDaily(
            dict,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 3));
        Assert.Equal(10, sum.DownloadBytes);
        Assert.Equal(20, sum.UploadBytes);
    }

    [Fact]
    public void SumDaily_EmptyMonth_ReturnsEmpty()
    {
        var dict = new Dictionary<DateOnly, TrafficUsage>();
        var sum = HistoryRangeCalculator.SumDaily(
            dict,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 2));
        Assert.Equal(0, sum.TotalBytes);
    }
}
