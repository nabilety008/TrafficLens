using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;
using TrafficLens.Core.Models;
using TrafficLens.Core.Selection;
using TrafficLens.Network.Process;

namespace TrafficLens.App.Tests;

public sealed class ApplicationsViewModelTests : IDisposable
{
    private readonly FakeProcessCollector _collector = new();
    private readonly LocalizationService _localization = new();
    private readonly ProcessIconResolver _icons = new();
    private readonly ApplicationsViewModel _vm;

    public ApplicationsViewModelTests()
    {
        _localization.SetCulture("en-US");
        _vm = new ApplicationsViewModel(_collector, _localization, _icons);
    }

    public void Dispose() => _vm.Dispose();

    private static ProcessTrafficSample Sample(
        int pid,
        string name,
        long startTicks,
        double downRate = 0,
        double upRate = 0,
        long downBytes = 0,
        long upBytes = 0,
        bool? isRunning = true) =>
        new(pid, startTicks, name, $@"C:\fake\{name}.exe", true, downBytes, upBytes,
            downRate, upRate, DateTime.UtcNow, isRunning);

    [Fact]
    public void Constructor_DoesNotAutoStartCollector()
    {
        Assert.Equal(0, _collector.StartCalls);
        Assert.False(_vm.IsStatusBannerVisible);
    }

    [Fact]
    public void Samples_ProduceOneRowPerProcessInstance()
    {
        _collector.PublishSamples(
            Sample(10, "chrome", 100, downRate: 100, downBytes: 500),
            Sample(11, "chrome", 200, downRate: 50, downBytes: 250),
            Sample(20, "svc", 300));

        Assert.Equal(3, _vm.Processes.Count);
        Assert.Equal(3, _vm.Rows.Count());
        Assert.Equal(10, _vm.Rows.Single(r => r.Name == "chrome" && r.Identity.ProcessId == 10).Identity.ProcessId);
        Assert.Equal(11, _vm.Rows.Single(r => r.Identity.ProcessId == 11).Identity.ProcessId);
    }

    [Fact]
    public void SamePidDifferentStartTimes_RemainDistinctRows()
    {
        _collector.PublishSamples(
            Sample(7, "app", 10_000, downRate: 100, downBytes: 400),
            Sample(7, "app", 90_000, downRate: 200, downBytes: 800));

        Assert.Equal(2, _vm.Processes.Count);
        Assert.Contains(_vm.Rows, r => r.Identity.StartTimeUtcTicks == 10_000);
        Assert.Contains(_vm.Rows, r => r.Identity.StartTimeUtcTicks == 90_000);
    }

    [Fact]
    public void RepublishedIdentity_UpdatesInPlace_NoDuplicateRows()
    {
        _collector.PublishSamples(
            Sample(1, "app", 1_000, downRate: 100, downBytes: 100));
        _collector.PublishSamples(
            Sample(1, "app", 1_000, downRate: 300, downBytes: 250));

        var row = Assert.Single(_vm.Processes);
        Assert.Equal("250 B", row.DownloadText);
        Assert.StartsWith("300", row.DownloadRateText);
        Assert.Single(_vm.Rows);
    }

    [Fact]
    public void DefaultSort_OrdersByTotalRateDescending()
    {
        _collector.PublishSamples(
            Sample(1, "a", 100, downRate: 100, upRate: 50),
            Sample(2, "b", 200, downRate: 30, upRate: 30),
            Sample(3, "c", 300, downRate: 200, upRate: 5));

        Assert.Equal("c", _vm.Processes[0].Name);
        Assert.Equal(3, _vm.Processes[0].Identity.ProcessId);
        Assert.Equal("b", _vm.Processes[2].Name);
    }

    [Fact]
    public void SortKey_SwitchesToUploadRate_DominantUploaderFirst()
    {
        _collector.PublishSamples(
            Sample(1, "downer", 100, downRate: 900, upRate: 10),
            Sample(2, "upper", 200, downRate: 10, upRate: 800));

        _vm.SortKey = ProcessSortKey.UploadRate;

        Assert.Equal("upper", _vm.Processes[0].Name);
    }

    [Fact]
    public void Search_ByNameFiltersRowsCaseInsensitive()
    {
        _collector.PublishSamples(
            Sample(1, "chrome", 100, downRate: 100),
            Sample(2, "ChromeHelper", 200, downRate: 100),
            Sample(3, "svchost", 300, downRate: 100));

        _vm.SearchText = "chrome";

        Assert.Equal(2, _vm.Processes.Count);
        Assert.All(_vm.Processes, p => Assert.True(p.Name.Contains("chrome", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Search_ByPidMatchesExactNumericPrefix()
    {
        _collector.PublishSamples(
            Sample(1234, "app", 100, downRate: 100),
            Sample(4321, "svc", 200, downRate: 100));

        _vm.SearchText = "123";

        var row = Assert.Single(_vm.Processes);
        Assert.Equal(1234, row.Identity.ProcessId);
    }

    [Fact]
    public void SearchAndSort_MergeWithoutMutatingCollector()
    {
        _collector.PublishSamples(
            Sample(1, "alpha", 100, downRate: 100, upRate: 50),
            Sample(2, "beta", 200, downRate: 200, upRate: 20));

        Assert.Equal(2, _collector.GetCurrentSamples().Count);

        _vm.SearchText = "beta";
        _vm.SortKey = ProcessSortKey.DownloadRate;

        Assert.Equal("beta", Assert.Single(_vm.Processes).Name);
        Assert.Equal(2, _collector.GetCurrentSamples().Count);
    }

    [Fact]
    public void TopConsumer_TotalsDownloadAndUpload()
    {
        _collector.PublishSamples(
            Sample(1, "down", 100, downRate: 800, upRate: 10),
            Sample(2, "up", 200, downRate: 50, upRate: 600),
            Sample(3, "both", 300, downRate: 400, upRate: 500));

        Assert.Equal("both", _vm.TopConsumerName);
        Assert.Equal("400 B/s", _vm.TopConsumerDownloadRateText);
        Assert.Equal("500 B/s", _vm.TopConsumerUploadRateText);
        Assert.True(_vm.HasActiveTraffic);
        Assert.False(_vm.IsIdle);
    }

    [Fact]
    public void AllIdle_ShowsIdleState_ButKeepsRows()
    {
        _collector.PublishSamples(
            Sample(1, "app", 100, downBytes: 120),
            Sample(2, "svc", 200, downBytes: 80));

        Assert.True(_vm.IsIdle);
        Assert.False(_vm.HasActiveTraffic);
        Assert.Equal(2, _vm.Processes.Count);
        Assert.Equal(string.Empty, _vm.TopConsumerName);
        Assert.Equal(string.Empty, _vm.TopDownloadName);
        Assert.Equal(string.Empty, _vm.TopUploadName);
    }

    [Fact]
    public void PermissionDenied_ShowsBannerWithRestartAction_NoAutoElevation()
    {
        _collector.SetStatus(ProcessTrafficCollectorStatus.PermissionDenied, "Elevation required");

        Assert.True(_vm.IsStatusBannerVisible);
        Assert.True(_vm.IsPermissionDenied);
        Assert.False(_vm.IsStartMonitoringVisible);
        Assert.Equal(0, _collector.StartCalls);
        Assert.Equal("Administrator permission is required to see per-process traffic.",
            _vm.StatusText);
        Assert.Equal("Elevation required", _vm.StatusDetailText);
    }

    [Fact]
    public void StoppedAndFailed_OfferStartMonitoring()
    {
        _collector.SetStatus(ProcessTrafficCollectorStatus.Stopped);
        Assert.True(_vm.IsStartMonitoringVisible);
        Assert.False(_vm.IsPermissionDenied);

        _vm.StartMonitoringCommand.Execute(null);
        Assert.Equal(1, _collector.StartCalls);
        Assert.Equal(ProcessTrafficCollectorStatus.Running, _vm.CollectorStatus);

        _collector.SetStatus(ProcessTrafficCollectorStatus.Failed, "boom");
        Assert.True(_vm.IsStartMonitoringVisible);
        Assert.Equal("boom", _vm.StatusDetailText);
    }

    [Fact]
    public void RunningStatus_HidesBanner()
    {
        _collector.SetStatus(ProcessTrafficCollectorStatus.Running);

        Assert.False(_vm.IsStatusBannerVisible);
        Assert.False(_vm.IsPermissionDenied);
        Assert.False(_vm.IsStartMonitoringVisible);
    }

    [Fact]
    public void CultureSwitch_RefreshesLocalizedLabels()
    {
        var en = _vm.ApplicationsLabel;
        _localization.SetCulture("fa-IR");

        Assert.NotEqual(en, _vm.ApplicationsLabel);
        Assert.NotEqual(en, _vm.SortByLabel);
    }

    [Fact]
    public void RunningAndExitedStates_MapToLocalizedText()
    {
        _collector.PublishSamples(
            Sample(1, "live", 100, isRunning: true),
            Sample(2, "gone", 200, isRunning: false),
            Sample(3, "unknown", 300, isRunning: null));

        Assert.Equal("Running", _vm.Processes.Single(p => p.Identity.ProcessId == 1).StateText);
        Assert.Equal("Exited", _vm.Processes.Single(p => p.Identity.ProcessId == 2).StateText);
        Assert.Equal(string.Empty, _vm.Processes.Single(p => p.Identity.ProcessId == 3).StateText);
    }
}