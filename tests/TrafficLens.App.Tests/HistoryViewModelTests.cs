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

    [Fact]
    public void Constructor_EmptySnapshot_ShowsZeros_NoDataState()
    {
        SetSnapshot(new HistorySnapshot(
            true, null,
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            TrafficUsage.Empty, Array.Empty<DailyUsagePoint>()));

        Assert.True(_vm.HasData == false);
        Assert.True(_vm.IsEmpty);
        Assert.False(_vm.IsUnavailable);
        Assert.Equal("0 B", _vm.SummaryDownloadText);
        Assert.Equal("0 B", _vm.SummaryTotalText);
    }

    [Fact]
    public void TodayRange_ShowsTodayTotals_AndSingleDaySeries()
    {
        SetSnapshot(new HistorySnapshot(
            true, null,
            new TrafficUsage(2048, 512),
            new TrafficUsage(100, 50),
            new TrafficUsage(4096, 1024),
            new TrafficUsage(8192, 2048),
            new TrafficUsage(16384, 4096),
            new[] { Day(13, 512, 128), Day(14, 1024, 256), Day(15, 2048, 512) }));

        Assert.Equal("Today", _vm.TodayLabel);
        Assert.Equal("2 KB", _vm.SummaryDownloadText);
        Assert.Equal("512 B", _vm.SummaryUploadText);
        Assert.Equal("2.5 KB", _vm.SummaryTotalText);
        var series = Assert.Single(_vm.Series);
        Assert.Equal(new DateOnly(2026, 6, 15), series.Date);
        Assert.True(_vm.HasData);
    }

    [Fact]
    public void YesterdayRange_SelectsPreviousDay()
    {
        _history.Snapshot = CreateSnapshot(yesterday: new TrafficUsage(300, 200));
        _vm.SelectRangeCommand.Execute(((int)HistoryRange.Yesterday).ToString());

        Assert.True(_vm.IsYesterdaySelected);
        Assert.Equal("300 B", _vm.SummaryDownloadText);
        var series = Assert.Single(_vm.Series);
        Assert.Equal(new DateOnly(2026, 6, 14), series.Date);
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
            points));
        _vm.SelectRangeCommand.Execute(((int)HistoryRange.Last7Days).ToString());

        Assert.True(_vm.IsLast7DaysSelected);
        Assert.Equal(7, _vm.Series.Count);
        Assert.Equal(new DateOnly(2026, 6, 24), _vm.Series[0].Date);
        Assert.Equal(new DateOnly(2026, 6, 30), _vm.Series[^1].Date);
    }

    [Fact]
    public void LifetimeRange_UsesAllSeriesPoints()
    {
        var points = new[] { Day(1, 100, 0), Day(2, 200, 0) };
        _history.Snapshot = new HistorySnapshot(
            true, null,
            TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
            new TrafficUsage(300, 0),
            points);
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
            new[] { Day(14, 100, 50), Day(15, 200, 100) });

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