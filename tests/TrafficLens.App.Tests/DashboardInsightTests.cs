using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.History;
using TrafficLens.Core.Models;

namespace TrafficLens.App.Tests;

public sealed class DashboardInsightTests : IDisposable
{
    private readonly FakeCollector _collector = new();
    private readonly FakeAdapterProvider _provider = new();
    private readonly LocalizationService _localization = new();
    private readonly FakeHistoryService _history = new();
    private readonly FakeProcessCollector _processCollector = new();
    private readonly DashboardViewModel _vm;

    public DashboardInsightTests()
    {
        _localization.SetCulture("en-US");
        _vm = new DashboardViewModel(_collector, _provider, _localization, _history, _processCollector);
        _vm.SetActive(true);
    }

    public void Dispose() => _vm.Dispose();

    #region Today Tests

    [Fact]
    public void Today_WithUsage_ShowsFormattedBytes()
    {
        _history.Snapshot = MakeSnapshot(new TrafficUsage(DownloadBytes: 1_073_741_824, UploadBytes: 536_870_912));

        _vm.RefreshToday();

        Assert.True(_vm.HasTodayData);
        Assert.Equal("1 GB", _vm.TodayDownloadText);
        Assert.Equal("512 MB", _vm.TodayUploadText);
        Assert.Equal("1.5 GB", _vm.TodayTotalText);
    }

    [Fact]
    public void Today_EmptyUsage_ShowsZeroBytes()
    {
        _history.Snapshot = MakeSnapshot(TrafficUsage.Empty);

        _vm.RefreshToday();

        Assert.True(_vm.HasTodayData);
        Assert.Equal("0 B", _vm.TodayDownloadText);
        Assert.Equal("0 B", _vm.TodayUploadText);
        Assert.Equal("0 B", _vm.TodayTotalText);
    }

    [Fact]
    public void Today_HistoryUnavailable_HidesTodayData()
    {
        _history.IsAvailable = false;

        _vm.RefreshToday();

        Assert.False(_vm.HasTodayData);
        Assert.Equal("0 B", _vm.TodayDownloadText);
        Assert.Equal("0 B", _vm.TodayUploadText);
        Assert.Equal("0 B", _vm.TodayTotalText);
    }

    [Fact]
    public void Today_SnapshotUnavailable_HidesTodayData()
    {
        _history.Snapshot = HistorySnapshot.Unavailable("db locked");

        _vm.RefreshToday();

        Assert.False(_vm.HasTodayData);
    }

    [Fact]
    public void Today_HistoryChangedEvent_Refreshes()
    {
        _history.Snapshot = MakeSnapshot(new TrafficUsage(DownloadBytes: 2_000_000, UploadBytes: 1_000_000));

        _history.RaiseChanged();

        Assert.True(_vm.HasTodayData);
        Assert.Equal(DataSizeFormatter.Format(2_000_000), _vm.TodayDownloadText);
        Assert.Equal(DataSizeFormatter.Format(1_000_000), _vm.TodayUploadText);
        Assert.Equal(DataSizeFormatter.Format(3_000_000), _vm.TodayTotalText);
    }

    [Fact]
    public void Today_Labels_AreLocalized()
    {
        _localization.SetCulture("fa-IR");

        Assert.Equal("مرور امروز", _vm.TodayAtGlanceLabel);
        Assert.Equal("دانلود امروز", _vm.DownloadTodayLabel);
        Assert.Equal("آپلود امروز", _vm.UploadTodayLabel);
        Assert.Equal("مجموع امروز", _vm.TotalTodayLabel);
    }

    [Fact]
    public void Today_MediumSize_FormatsMegabytes()
    {
        _history.Snapshot = MakeSnapshot(new TrafficUsage(DownloadBytes: 5_242_880, UploadBytes: 2_097_152));

        _vm.RefreshToday();

        Assert.Equal("5 MB", _vm.TodayDownloadText);
        Assert.Equal("2 MB", _vm.TodayUploadText);
        Assert.Equal("7 MB", _vm.TodayTotalText);
    }

    #endregion

    #region Top App Tests

    [Fact]
    public void TopApp_WithActiveProcess_ShowsNameAndRates()
    {
        var sample = ProcessSample(1, "chrome.exe", dlRate: 1_048_576, ulRate: 524_288);
        _processCollector.PublishSamples(sample);

        Assert.True(_vm.HasTopApp);
        Assert.Equal("chrome.exe", _vm.TopAppName);
        Assert.Equal(DataRateFormatter.FormatAdaptive(1_048_576), _vm.TopAppDownloadRateText);
        Assert.Equal(DataRateFormatter.FormatAdaptive(524_288), _vm.TopAppUploadRateText);
        Assert.Equal(DataRateFormatter.FormatAdaptive(1_572_864), _vm.TopAppRateText);
    }

    [Fact]
    public void TopApp_AllIdle_ShowsIdleState()
    {
        var sample = ProcessSample(1, "chrome.exe", dlRate: 0, ulRate: 0);
        _processCollector.PublishSamples(sample);

        Assert.False(_vm.HasTopApp);
        Assert.True(_vm.IsTopAppIdle);
        Assert.Equal(_vm.TopAppNoDataLabel, _vm.TopAppStatusText);
    }

    [Fact]
    public void TopApp_EmptySamples_ShowsIdleState()
    {
        _processCollector.PublishSamples();

        Assert.False(_vm.HasTopApp);
        Assert.True(_vm.IsTopAppIdle);
    }

    [Fact]
    public void TopApp_PermissionDenied_ShowsWarning()
    {
        _processCollector.SetStatus(ProcessTrafficCollectorStatus.PermissionDenied);

        Assert.True(_vm.IsTopAppPermissionDenied);
        Assert.False(_vm.HasTopApp);
        Assert.Equal(_vm.TopAppPermissionDeniedLabel, _vm.TopAppStatusText);
    }

    [Fact]
    public void TopApp_CollectorFailed_ShowsUnavailable()
    {
        _processCollector.SetStatus(ProcessTrafficCollectorStatus.Failed, "ETW error");

        Assert.True(_vm.IsTopAppUnavailable);
        Assert.False(_vm.HasTopApp);
        Assert.Equal(_vm.TopAppUnavailableLabel, _vm.TopAppStatusText);
    }

    [Fact]
    public void TopApp_Stopped_ShowsUnavailable()
    {
        _processCollector.SetStatus(ProcessTrafficCollectorStatus.Stopped);

        Assert.True(_vm.IsTopAppUnavailable);
        Assert.False(_vm.HasTopApp);
        Assert.Equal(_vm.TopAppUnavailableLabel, _vm.TopAppStatusText);
    }

    [Fact]
    public void TopApp_SamplesReadyEvent_Updates()
    {
        var sample = ProcessSample(1, "msedge.exe", dlRate: 2_097_152, ulRate: 1_048_576);

        _processCollector.PublishSamples(sample);

        Assert.True(_vm.HasTopApp);
        Assert.Equal("msedge.exe", _vm.TopAppName);
        Assert.Equal(DataRateFormatter.FormatAdaptive(2_097_152), _vm.TopAppDownloadRateText);
    }

    [Fact]
    public void TopApp_TopConsumerPicksHighestTotalRate()
    {
        var chrome = ProcessSample(1, "chrome.exe", dlRate: 1_000_000, ulRate: 100_000);
        var edge = ProcessSample(2, "msedge.exe", dlRate: 2_000_000, ulRate: 50_000);
        var slack = ProcessSample(3, "slack.exe", dlRate: 50_000, ulRate: 200_000);

        _processCollector.PublishSamples(chrome, edge, slack);

        Assert.True(_vm.HasTopApp);
        Assert.Equal("msedge.exe", _vm.TopAppName);
    }

    [Fact]
    public void TopApp_PermissionDeniedThenRecovered_ClearsWarning()
    {
        _processCollector.SetStatus(ProcessTrafficCollectorStatus.PermissionDenied);
        Assert.True(_vm.IsTopAppPermissionDenied);

        _processCollector.SetStatus(ProcessTrafficCollectorStatus.Running);
        var sample = ProcessSample(1, "teams.exe", dlRate: 100_000, ulRate: 50_000);
        _processCollector.PublishSamples(sample);

        Assert.False(_vm.IsTopAppPermissionDenied);
        Assert.True(_vm.HasTopApp);
        Assert.Equal("teams.exe", _vm.TopAppName);
    }

    [Fact]
    public void TopApp_Labels_AreLocalized()
    {
        _localization.SetCulture("fa-IR");

        Assert.Equal("برترین برنامه الان", _vm.TopAppNowLabel);
        Assert.Equal("برنامه", _vm.TopAppApplicationLabel);
        Assert.Equal("سرعت فعلی", _vm.TopAppCurrentLabel);
        Assert.Equal("ترافیک فعالی وجود ندارد", _vm.TopAppNoDataLabel);
    }

    [Fact]
    public void TopApp_NoHistoryService_HidesSection()
    {
        var vmNoHistory = new DashboardViewModel(_collector, _provider, _localization, null, null);
        vmNoHistory.SetActive(true);

        Assert.False(vmNoHistory.HasTopApp);

        vmNoHistory.Dispose();
    }

    #endregion

    #region Lifecycle Tests

    [Fact]
    public void SetActive_False_DoesNotUpdateFromHistoryEvent()
    {
        _vm.SetActive(false);

        _history.Snapshot = MakeSnapshot(new TrafficUsage(DownloadBytes: 1_000_000, UploadBytes: 500_000));

        _history.RaiseChanged();

        Assert.False(_vm.HasTodayData);
    }

    [Fact]
    public void SetActive_True_ThenFalse_StopsProcessUpdates()
    {
        _vm.SetActive(false);

        var sample = ProcessSample(1, "test.exe", dlRate: 1_000_000, ulRate: 500_000);
        _processCollector.PublishSamples(sample);

        Assert.False(_vm.HasTopApp);

        _vm.SetActive(true);
        _processCollector.PublishSamples(sample);

        Assert.True(_vm.HasTopApp);
    }

    [Fact]
    public void Dispose_UnsubscribesFromEvents()
    {
        var vm = new DashboardViewModel(_collector, _provider, _localization, _history, _processCollector);
        vm.SetActive(true);

        var historySnapshot = MakeSnapshot(new TrafficUsage(DownloadBytes: 999, UploadBytes: 888));
        _history.Snapshot = historySnapshot;
        _history.RaiseChanged();
        Assert.True(vm.HasTodayData);

        vm.Dispose();

        _history.Snapshot = HistorySnapshot.Unavailable(null);
        _history.RaiseChanged();
        Assert.True(vm.HasTodayData);
    }

    [Fact]
    public void NoHistoryService_Constructor_DoesNotThrow()
    {
        var vmNoHistory = new DashboardViewModel(_collector, _provider, _localization);
        Assert.False(vmNoHistory.HasTodayData);
        Assert.False(vmNoHistory.HasTopApp);
        vmNoHistory.Dispose();
    }

    #endregion

    private static HistorySnapshot MakeSnapshot(TrafficUsage today) => new(
        IsAvailable: true,
        Error: null,
        Today: today,
        Yesterday: TrafficUsage.Empty,
        Last7Days: TrafficUsage.Empty,
        Last30Days: TrafficUsage.Empty,
        Lifetime: TrafficUsage.Empty,
        DailySeries: Array.Empty<DailyUsagePoint>(),
        TodayHourly: Array.Empty<HourlyUsagePoint>());

    private static ProcessTrafficSample ProcessSample(
        int pid,
        string name,
        double dlRate,
        double ulRate) =>
        new(
            ProcessId: pid,
            ProcessStartTimeUtcTicks: 0,
            ProcessName: name,
            ExecutablePath: null,
            IconAvailable: false,
            DownloadBytes: 0,
            UploadBytes: 0,
            DownloadBytesPerSecond: dlRate,
            UploadBytesPerSecond: ulRate,
            Timestamp: DateTime.UtcNow);
}
