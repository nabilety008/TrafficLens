using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;
using TrafficLens.Core.History;

namespace TrafficLens.App.Tests;

public sealed class HistoryViewModelTests : IDisposable
{
    private readonly FakeHistoryService _history = new();
    private readonly LocalizationService _localization = new();
    private readonly HistoryViewModel _vm;

    public HistoryViewModelTests()
    {
        _localization.SetCulture("en-US");
        _vm = new HistoryViewModel(_history, _localization);
    }

    public void Dispose() => _vm.Dispose();

    private static DailyUsagePoint Day(int day, long download, long upload) =>
        new(new DateOnly(2026, 6, day), download, upload);

    private static HourlyUsagePoint Hour(int hour, long download, long upload)
    {
        var startUtc = new DateTime(2026, 6, 15, hour, 0, 0, DateTimeKind.Utc);
        return new HourlyUsagePoint(startUtc, startUtc.AddHours(1), hour, download, upload);
    }

    [Fact]
    public void Constructor_EmptySnapshot_ShowsZeros_NoDataState()
    {
        SetSnapshot(new HistorySnapshot(
            true, null,
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            TrafficUsage.Empty, Array.Empty<DailyUsagePoint>(), Array.Empty<HourlyUsagePoint>(),
            TrafficUsage.Empty, 0));

        Assert.True(_vm.HasData == false);
        Assert.True(_vm.IsEmpty);
        Assert.False(_vm.IsUnavailable);
        Assert.Equal("0 B", _vm.SummaryDownloadText);
        Assert.Equal("0 B", _vm.SummaryTotalText);
    }

    [Fact]
    public void TodayRange_ShowsTodayTotals_AndUsesDailySeriesWhenNoHourlyData()
    {
        SetSnapshot(new HistorySnapshot(
            true, null,
            new TrafficUsage(2048, 512),
            new TrafficUsage(100, 50),
            new TrafficUsage(4096, 1024),
            new TrafficUsage(8192, 2048),
            new TrafficUsage(16384, 4096),
            new[] { Day(13, 512, 128), Day(14, 1024, 256), Day(15, 2048, 512) },
            Array.Empty<HourlyUsagePoint>(),
            new TrafficUsage(2048, 512), 0));

        Assert.Equal("Today", _vm.TodayLabel);
        Assert.Equal("2 KB", _vm.SummaryDownloadText);
        Assert.Equal("512 B", _vm.SummaryUploadText);
        Assert.Equal("2.5 KB", _vm.SummaryTotalText);
        var series = Assert.Single(_vm.Series);
        Assert.Equal("06-15", series.Label);
        Assert.True(_vm.HasData);
    }

    [Fact]
    public void TodayRange_WithHourlyData_UsesHourlySeries()
    {
        SetSnapshot(new HistorySnapshot(
            true, null,
            new TrafficUsage(2200, 800),
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            new TrafficUsage(2200, 800),
            new[] { Day(15, 2200, 800) },
            new[] { Hour(10, 100, 0), Hour(11, 2048, 512), Hour(12, 52, 288) },
            new TrafficUsage(2200, 800), 0));

        Assert.Equal("10:00", _vm.Series[0].Label);
        Assert.Equal("11:00", _vm.Series[1].Label);
        Assert.Equal("12:00", _vm.Series[2].Label);
        Assert.Equal(3, _vm.Series.Count);
        Assert.Equal(2560, _vm.ScaleMax);
        Assert.Equal("2.15 KB", _vm.SummaryDownloadText);
        Assert.True(_vm.HasData);
    }

    [Fact]
    public void TodayRange_ChartTitle_IsHourly()
    {
        SetSnapshot(new HistorySnapshot(
            true, null,
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            TrafficUsage.Empty, Array.Empty<DailyUsagePoint>(), new[] { Hour(10, 1, 0) },
            TrafficUsage.Empty, 0));

        Assert.Equal(_localization["HistoryHourlyTrafficLabel"], _vm.ChartTitleLabel);
    }

    [Fact]
    public void TodayRange_HourlySeries_RendersEmptyHoursAsZeroBars()
    {
        SetSnapshot(new HistorySnapshot(
            true, null,
            new TrafficUsage(100, 0),
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            new[] { Day(15, 100, 0) },
            new[] { Hour(0, 40, 0), Hour(1, 0, 0), Hour(2, 60, 0) },
            TrafficUsage.Empty, 0));

        Assert.Equal(3, _vm.Series.Count);
        Assert.Equal(0, _vm.Series[1].TotalBytes);
        Assert.Equal(60, _vm.ScaleMax);
    }

    [Fact]
    public void YesterdayRange_SelectsPreviousDay()
    {
        _history.Snapshot = CreateSnapshot(yesterday: new TrafficUsage(300, 200));
        _vm.SelectRangeCommand.Execute(((int)HistoryRange.Yesterday).ToString());

        Assert.True(_vm.IsYesterdaySelected);
        Assert.Equal("300 B", _vm.SummaryDownloadText);
        var series = Assert.Single(_vm.Series);
        Assert.Equal("06-14", series.Label);
        Assert.Equal(_localization["HistoryDailyTrafficLabel"], _vm.ChartTitleLabel);
    }

    [Fact]
    public void Last7DaysRange_SlicesLastSevenPoints()
    {
        var points = Enumerable.Range(1, 30)
            .Select(d => Day(d, d * 100, d * 10))
            .ToArray();
        SetSnapshot(new HistorySnapshot(
            true, null,
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            new TrafficUsage(1550, 0),
            new TrafficUsage(0, 0),
            points,
            Array.Empty<HourlyUsagePoint>(),
            TrafficUsage.Empty, 0));
        _vm.SelectRangeCommand.Execute(((int)HistoryRange.Last7Days).ToString());

        Assert.True(_vm.IsLast7DaysSelected);
        Assert.Equal(7, _vm.Series.Count);
        Assert.Equal("06-24", _vm.Series[0].Label);
        Assert.Equal("06-30", _vm.Series[^1].Label);
    }

    [Fact]
    public void LifetimeRange_UsesAllSeriesPoints()
    {
        var points = new[] { Day(1, 100, 0), Day(2, 200, 0) };
        _history.Snapshot = new HistorySnapshot(
            true, null,
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            new TrafficUsage(300, 0),
            points,
            Array.Empty<HourlyUsagePoint>(),
            TrafficUsage.Empty, 0);
        _vm.SelectRangeCommand.Execute(((int)HistoryRange.Lifetime).ToString());

        Assert.True(_vm.IsLifetimeSelected);
        Assert.Equal("300 B", _vm.SummaryTotalText);
        Assert.Equal(2, _vm.Series.Count);
    }

    [Fact]
    public void UnavailableSnapshot_SetsBannerState()
    {
        SetSnapshot(HistorySnapshot.Unavailable("disk full"));
        Assert.True(_vm.IsUnavailable);
        Assert.Equal(_localization["HistoryErrorDetailLabel"], _vm.ErrorDetail);
        Assert.DoesNotContain("disk full", _vm.ErrorDetail);
        Assert.True(_vm.IsEmpty);
    }

    [Fact]
    public void CultureSwitch_ReappliesSnapshot_UpdatesChartTitle()
    {
        SetSnapshot(new HistorySnapshot(
            true, null,
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            TrafficUsage.Empty, Array.Empty<DailyUsagePoint>(), new[] { Hour(10, 1, 0) },
            TrafficUsage.Empty, 0));
        Assert.Equal(_localization["HistoryHourlyTrafficLabel"], _vm.ChartTitleLabel);

        _localization.SetCulture("fa-IR");

        Assert.Equal("ترافیک ساعتی", _vm.ChartTitleLabel);
    }

    [Fact]
    public void ThisMonthRange_SlicesCurrentMonthDays()
    {
        var points = new[]
        {
            Day(1, 100, 50),
            Day(14, 200, 100),
            Day(15, 300, 150),
        };
        SetSnapshot(new HistorySnapshot(
            true, null,
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            new TrafficUsage(600, 300),
            new TrafficUsage(600, 300),
            points,
            Array.Empty<HourlyUsagePoint>(),
            new TrafficUsage(300, 150), 0));
        _vm.SelectRangeCommand.Execute(((int)HistoryRange.ThisMonth).ToString());

        Assert.True(_vm.IsThisMonthSelected);
        Assert.Equal("300 B", _vm.SummaryDownloadText);
        Assert.Equal("150 B", _vm.SummaryUploadText);
        Assert.Equal("450 B", _vm.SummaryTotalText);
        Assert.Equal(3, _vm.Series.Count);
    }

    [Fact]
    public void ThisMonthRange_Label_IsLocalized()
    {
        _localization.SetCulture("fa-IR");
        Assert.Equal("این ماه", _vm.ThisMonthLabel);
    }

    [Fact]
    public void Comparison_ShowsWhenTodaySelected_WithData()
    {
        var snapshot = new HistorySnapshot(
            true, null,
            new TrafficUsage(1000, 500),
            new TrafficUsage(2000, 1000),
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            new[] { Day(14, 2000, 1000), Day(15, 1000, 500) },
            Array.Empty<HourlyUsagePoint>(),
            new TrafficUsage(1000, 500), 0.5);
        SetSnapshot(snapshot);

        Assert.True(_vm.HasComparison);
        Assert.Contains("1.46 KB", _vm.ComparisonTodayText);
        Assert.Contains("1.46 KB", _vm.ComparisonYesterdayText);
    }

    [Fact]
    public void Comparison_HiddenWhenYesterdayZero()
    {
        var snapshot = new HistorySnapshot(
            true, null,
            new TrafficUsage(1000, 500),
            TrafficUsage.Empty,
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            new[] { Day(15, 1000, 500) },
            Array.Empty<HourlyUsagePoint>(),
            new TrafficUsage(1000, 500), 0.5);
        SetSnapshot(snapshot);

        Assert.False(_vm.HasComparison);
    }

    [Fact]
    public void Comparison_HiddenWhenDayFractionZero()
    {
        var snapshot = new HistorySnapshot(
            true, null,
            new TrafficUsage(1000, 500),
            new TrafficUsage(2000, 1000),
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            new[] { Day(15, 1000, 500) },
            Array.Empty<HourlyUsagePoint>(),
            new TrafficUsage(1000, 500), 0);
        SetSnapshot(snapshot);

        Assert.False(_vm.HasComparison);
    }

    [Fact]
    public void Comparison_NegativeDifference()
    {
        var snapshot = new HistorySnapshot(
            true, null,
            new TrafficUsage(500, 250),
            new TrafficUsage(2000, 1000),
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            new[] { Day(14, 2000, 1000), Day(15, 500, 250) },
            Array.Empty<HourlyUsagePoint>(),
            new TrafficUsage(500, 250), 0.5);
        SetSnapshot(snapshot);

        Assert.True(_vm.HasComparison);
        Assert.Contains("−", _vm.ComparisonDifferenceText);
        Assert.Contains("-", _vm.ComparisonPercentageText);
    }

    [Fact]
    public void Comparison_EqualValues_ShowsZeroDifference()
    {
        var snapshot = new HistorySnapshot(
            true, null,
            new TrafficUsage(1000, 500),
            new TrafficUsage(1000, 500),
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            new[] { Day(14, 1000, 500), Day(15, 1000, 500) },
            Array.Empty<HourlyUsagePoint>(),
            new TrafficUsage(1000, 500), 1.0);
        SetSnapshot(snapshot);

        Assert.True(_vm.HasComparison);
        Assert.Contains("0 B", _vm.ComparisonDifferenceText);
        Assert.Contains("0%", _vm.ComparisonPercentageText);
    }

    [Fact]
    public void Comparison_NotShownWhenYesterdayIsZero()
    {
        var snapshot = new HistorySnapshot(
            true, null,
            new TrafficUsage(1000, 500),
            TrafficUsage.Empty,
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            new[] { Day(15, 1000, 500) },
            Array.Empty<HourlyUsagePoint>(),
            new TrafficUsage(1000, 500), 0.5);
        SetSnapshot(snapshot);

        Assert.False(_vm.HasComparison);
    }

    [Fact]
    public void Comparison_DayFraction_25Percent()
    {
        var snapshot = new HistorySnapshot(
            true, null,
            new TrafficUsage(100, 50),
            new TrafficUsage(400, 200),
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            new[] { Day(14, 400, 200), Day(15, 100, 50) },
            Array.Empty<HourlyUsagePoint>(),
            new TrafficUsage(100, 50), 0.25);
        SetSnapshot(snapshot);

        Assert.True(_vm.HasComparison);
        Assert.Contains("150 B", _vm.ComparisonTodayText);
        Assert.Contains("150 B", _vm.ComparisonYesterdayText);
    }

    private static HistorySnapshot CreateSnapshot(
        TrafficUsage today = default,
        TrafficUsage yesterday = default) =>
        new(
            true,
            null,
            today,
            yesterday,
            TrafficUsage.Empty,
            TrafficUsage.Empty,
            TrafficUsage.Empty,
            new[] { Day(14, 100, 50), Day(15, 200, 100) },
            Array.Empty<HourlyUsagePoint>(),
            TrafficUsage.Empty,
            0);

    private sealed class FakeHistoryService : ITrafficHistoryService
    {
        public event EventHandler? HistoryChanged;
        public bool IsAvailable { get; set; } = true;
        public string? LastError { get; set; }
        public HistorySnapshot Snapshot { get; set; } = HistorySnapshot.Unavailable(null);
        public HistorySnapshot GetSnapshot() => Snapshot;
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
        public void RaiseChanged() => HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetSnapshot(HistorySnapshot snapshot)
    {
        _history.Snapshot = snapshot;
        _history.RaiseChanged();
    }
}