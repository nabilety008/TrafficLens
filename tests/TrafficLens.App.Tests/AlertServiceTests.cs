using Microsoft.Extensions.Logging.Abstractions;
using TrafficLens.App.Services;
using TrafficLens.Core.Alerts;
using TrafficLens.Core.History;
using TrafficLens.Core.Models;

namespace TrafficLens.App.Tests;

public sealed class AlertServiceTests
{
    private const double OneMb = 1024 * 1024;
    private const double OneGb = OneMb * 1024;

    private static readonly NetworkAdapterInfo UpAdapter = new(
        "eth0", "Ethernet", "desc", "00:11:22:33:44:55",
        NetworkAdapterKind.Ethernet, IsUp: true, IsDefault: true);

    private static readonly DateTime Timestamp = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static NetworkSpeedSample Sample(string id, double download, double upload) =>
        new(id, id, (long)download, (long)upload, Timestamp);

    private static AlertService CreateService(
        FakeCollector collector,
        FakeAdapterProvider adapters,
        FakeHistoryService history,
        FakeSettingsService settings,
        out List<AlertEvent> raised)
    {
        var events = new List<AlertEvent>();
        var service = new AlertService(
            collector,
            adapters,
            history,
            settings,
            NullLogger<AlertService>.Instance);
        service.AlertRaised += (_, args) => events.Add(args.Alert);
        raised = events;
        return service;
    }

    private static void SetupHighDownload(FakeSettingsService settings)
    {
        settings.Set(AlertSettings.HighDownloadSpeedEnabledKey, "true");
        settings.Set(AlertSettings.HighDownloadSpeedThresholdKey, $"{10 * OneMb}");
    }

    private static void SetupDailyTotal(FakeSettingsService settings, double limit = 10 * OneGb)
    {
        settings.Set(AlertSettings.DailyTotalLimitEnabledKey, "true");
        settings.Set(AlertSettings.DailyTotalLimitThresholdKey, $"{limit}");
    }

    [Fact]
    public void Constructor_AppliesSettingsDefaults()
    {
        var service = CreateService(
            new FakeCollector(),
            new FakeAdapterProvider(),
            new FakeHistoryService(),
            new FakeSettingsService(),
            out _);

        Assert.Empty(service.RecentAlerts);
        service.Dispose();
    }

    [Fact]
    public void SpeedSample_AboveThreshold_RaisesAlert_Once()
    {
        var collector = new FakeCollector();
        var adapters = new FakeAdapterProvider();
        adapters.SetAdapters(new[] { UpAdapter });
        var history = new FakeHistoryService();
        var settings = new FakeSettingsService();
        SetupHighDownload(settings);

        using var service = CreateService(collector, adapters, history, settings, out var raised);

        collector.RaiseSample(Sample("eth0", 0, 0));
        Assert.Empty(raised);

        collector.RaiseSample(Sample("eth0", 15 * OneMb, 0));
        var alert = Assert.Single(raised);
        Assert.Equal(AlertType.HighDownloadSpeed, alert.Type);
        Assert.Equal(15 * OneMb, alert.Value);
        Assert.Equal(10 * OneMb, alert.Threshold);

        Assert.Single(service.RecentAlerts);

        collector.RaiseSample(Sample("eth0", 20 * OneMb, 0));
        Assert.Single(raised);
    }

    [Fact]
    public void SpeedSample_DisabledRule_NeverRaises()
    {
        var collector = new FakeCollector();
        var adapters = new FakeAdapterProvider();
        adapters.SetAdapters(new[] { UpAdapter });

        using var service = CreateService(
            collector, adapters, new FakeHistoryService(), new FakeSettingsService(), out var raised);

        collector.RaiseSample(Sample("eth0", 900 * OneMb, 900 * OneMb));
        Assert.Empty(raised);
    }

    [Fact]
    public void SpeedSample_NoUpAdapters_NoAlert()
    {
        var collector = new FakeCollector();
        var adapters = new FakeAdapterProvider();
        adapters.SetAdapters(new[] { new NetworkAdapterInfo(
            "eth0", "Ethernet", "desc", "00:11:22:33:44:55",
            NetworkAdapterKind.Ethernet, IsUp: false, IsDefault: false) });
        var settings = new FakeSettingsService();
        SetupHighDownload(settings);

        using var service = CreateService(
            collector, adapters, new FakeHistoryService(), settings, out var raised);

        collector.RaiseSample(Sample("eth0", 15 * OneMb, 0));
        Assert.Empty(raised);
    }

    [Fact]
    public void HistoryChanged_AboveDailyLimit_RaisesAndPersistsTriggeredDate()
    {
        var history = new FakeHistoryService
        {
            Snapshot = new HistorySnapshot(
                true, null,
                new TrafficUsage((long)(12 * OneGb), 0),
                TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
                Array.Empty<DailyUsagePoint>())
        };
        var settings = new FakeSettingsService();
        SetupDailyTotal(settings);

        using var service = CreateService(
            new FakeCollector(), new FakeAdapterProvider(), history, settings, out var raised);

        history.RaiseChanged();
        Assert.Single(raised);

        var today = DateOnly.FromDateTime(DateTime.Now);
        Assert.Equal(
            today.ToString("yyyy-MM-dd"),
            settings.Get(AlertSettings.LastTriggeredDailyTotalKey, string.Empty));
    }

    [Fact]
    public void HistoryUnavailable_DailySuspended_SpeedStillWorks()
    {
        var collector = new FakeCollector();
        var adapters = new FakeAdapterProvider();
        adapters.SetAdapters(new[] { UpAdapter });
        var history = new FakeHistoryService { Snapshot = HistorySnapshot.Unavailable("db down") };
        var settings = new FakeSettingsService();
        SetupHighDownload(settings);
        SetupDailyTotal(settings);

        using var service = CreateService(collector, adapters, history, settings, out var raised);

        history.RaiseChanged();
        Assert.Empty(raised);

        collector.RaiseSample(Sample("eth0", 15 * OneMb, 0));
        Assert.Single(raised);
        Assert.Equal(AlertType.HighDownloadSpeed, raised[0].Type);
    }

    [Fact]
    public void RestartSameDay_DoesNotReTriggerDaily()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var settings = new FakeSettingsService();
        SetupDailyTotal(settings);
        settings.Set(AlertSettings.LastTriggeredDailyTotalKey, today.ToString("yyyy-MM-dd"));

        var history = new FakeHistoryService
        {
            Snapshot = new HistorySnapshot(
                true, null,
                new TrafficUsage((long)(12 * OneGb), 0),
                TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty, TrafficUsage.Empty,
                Array.Empty<DailyUsagePoint>())
        };

        using var service = CreateService(
            new FakeCollector(), new FakeAdapterProvider(), history, settings, out var raised);

        history.RaiseChanged();
        Assert.Empty(raised);
    }

    [Fact]
    public void RefreshConfig_AppliesDisableChange()
    {
        var collector = new FakeCollector();
        var adapters = new FakeAdapterProvider();
        adapters.SetAdapters(new[] { UpAdapter });
        var settings = new FakeSettingsService();
        SetupHighDownload(settings);

        using var service = CreateService(
            collector, adapters, new FakeHistoryService(), settings, out var raised);

        collector.RaiseSample(Sample("eth0", 15 * OneMb, 0));
        Assert.Single(raised);

        settings.Set(AlertSettings.HighDownloadSpeedEnabledKey, "false");
        service.RefreshConfig();

        collector.RaiseSample(Sample("eth0", 900 * OneMb, 0));
        Assert.Single(raised);
    }

    [Fact]
    public void Dispose_StopsRaising()
    {
        var collector = new FakeCollector();
        var adapters = new FakeAdapterProvider();
        adapters.SetAdapters(new[] { UpAdapter });
        var settings = new FakeSettingsService();
        SetupHighDownload(settings);

        var service = CreateService(collector, adapters, new FakeHistoryService(), settings, out var raised);
        service.Dispose();

        collector.RaiseSample(Sample("eth0", 15 * OneMb, 0));
        Assert.Empty(raised);
    }
}