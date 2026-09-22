using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;
using TrafficLens.Core.Alerts;

namespace TrafficLens.App.Tests;

public sealed class AlertsViewModelTests : IDisposable
{
    private readonly FakeAlertService _alertService = new();
    private readonly LocalizationService _localization = new();
    private readonly AlertsViewModel _vm;

    public AlertsViewModelTests()
    {
        _localization.SetCulture("en-US");
        _vm = new AlertsViewModel(_alertService, _localization);
    }

    public void Dispose() => _vm.Dispose();

    private static AlertEvent Event(AlertType type, double value, double threshold) =>
        new(type, value, threshold, new DateTimeOffset(2026, 6, 1, 10, 30, 0, TimeSpan.Zero));

    [Fact]
    public void Constructor_EmptyBuffer_ShowsNoAlertsState()
    {
        Assert.True(_vm.HasNoAlerts);
        Assert.Equal("No alerts have been triggered yet.", _vm.NoAlertsText);
        Assert.Equal(string.Empty, _vm.AlertCountText);
        Assert.Empty(_vm.RecentAlerts);
    }

    [Fact]
    public void AlertRaised_AddsNewestFirstRow()
    {
        _alertService.Raise(Event(AlertType.HighDownloadSpeed, 52 * 1024 * 1024, 50 * 1024 * 1024));
        _alertService.Raise(Event(AlertType.DailyTotalLimit, 100L * 1024 * 1024 * 1024, 100L * 1024 * 1024 * 1024));

        var expectedTime = Event(AlertType.DailyTotalLimit, 0, 0).OccurredUtc.ToLocalTime().ToString("HH:mm:ss");

        Assert.False(_vm.HasNoAlerts);
        Assert.Equal("Alerts triggered: 2", _vm.AlertCountText);
        Assert.Equal(2, _vm.RecentAlerts.Count);
        Assert.Equal("Daily total limit", _vm.RecentAlerts[0].TypeText);
        Assert.Equal("Daily total usage limit reached: 100 GB today (limit 100 GB)", _vm.RecentAlerts[0].Message);
        Assert.Equal(expectedTime, _vm.RecentAlerts[0].TimeText);
    }

    [Fact]
    public void CultureChange_RelocalizesRows()
    {
        _alertService.Raise(Event(AlertType.HighDownloadSpeed, 60 * 1024 * 1024, 50 * 1024 * 1024));

        Assert.Contains("MB/s", _vm.RecentAlerts[0].Message);

        _localization.SetCulture("fa-IR");

        Assert.Equal("سرعت دانلود بالا", _vm.RecentAlerts[0].TypeText);
        Assert.Equal("تعداد هشدارها: 1", _vm.AlertCountText);
    }

    [Fact]
    public void Dispose_StopsListening()
    {
        _vm.Dispose();

        _alertService.Raise(Event(AlertType.HighDownloadSpeed, 60 * 1024 * 1024, 50 * 1024 * 1024));

        Assert.Empty(_vm.RecentAlerts);
        Assert.True(_vm.HasNoAlerts);
    }

    [Fact]
    public void Constructor_DefaultConfig_ShowsNoConfiguredRules_AllDisabled()
    {
        Assert.Empty(_vm.ConfiguredRules);
        Assert.True(_vm.HasNoConfiguredRules);
    }

    [Fact]
    public void ConfigChanged_UpdatesConfiguredRules()
    {
        _alertService.CurrentConfig = new AlertConfig(
            HighDownloadSpeedEnabled: true,
            HighDownloadSpeedThresholdBytesPerSecond: 50 * 1024 * 1024,
            HighUploadSpeedEnabled: false,
            HighUploadSpeedThresholdBytesPerSecond: 20 * 1024 * 1024,
            DailyDownloadLimitEnabled: true,
            DailyDownloadLimitBytes: 50L * 1024 * 1024 * 1024,
            DailyUploadLimitEnabled: false,
            DailyUploadLimitBytes: 20L * 1024 * 1024 * 1024,
            DailyTotalLimitEnabled: false,
            DailyTotalLimitBytes: 100L * 1024 * 1024 * 1024,
            Cooldown: TimeSpan.FromMinutes(5));

        _alertService.RefreshConfig();

        Assert.False(_vm.HasNoConfiguredRules);
        Assert.Equal(2, _vm.ConfiguredRules.Count);

        var highDownload = _vm.ConfiguredRules.First(r => r.Type == AlertType.HighDownloadSpeed);
        Assert.Contains("50", highDownload.ThresholdText);

        var dailyDownload = _vm.ConfiguredRules.First(r => r.Type == AlertType.DailyDownloadLimit);
        Assert.NotNull(dailyDownload);

        Assert.DoesNotContain(_vm.ConfiguredRules, r => r.Type == AlertType.HighUploadSpeed);
        Assert.DoesNotContain(_vm.ConfiguredRules, r => r.Type == AlertType.DailyUploadLimit);
        Assert.DoesNotContain(_vm.ConfiguredRules, r => r.Type == AlertType.DailyTotalLimit);
    }

    [Fact]
    public void ConfigChanged_CultureChange_LocalizesConfiguredRules()
    {
        _alertService.CurrentConfig = new AlertConfig(
            HighDownloadSpeedEnabled: true,
            HighDownloadSpeedThresholdBytesPerSecond: 50 * 1024 * 1024,
            HighUploadSpeedEnabled: false,
            HighUploadSpeedThresholdBytesPerSecond: 20 * 1024 * 1024,
            DailyDownloadLimitEnabled: false,
            DailyDownloadLimitBytes: 50L * 1024 * 1024 * 1024,
            DailyUploadLimitEnabled: false,
            DailyUploadLimitBytes: 20L * 1024 * 1024 * 1024,
            DailyTotalLimitEnabled: false,
            DailyTotalLimitBytes: 100L * 1024 * 1024 * 1024,
            Cooldown: TimeSpan.FromMinutes(5));

        _alertService.RefreshConfig();

        var highDownload = _vm.ConfiguredRules.First(r => r.Type == AlertType.HighDownloadSpeed);
        Assert.Equal("High download speed", highDownload.RuleName);

        _localization.SetCulture("fa-IR");

        var highDownloadFa = _vm.ConfiguredRules.First(r => r.Type == AlertType.HighDownloadSpeed);
        Assert.Equal("سرعت دانلود بالا", highDownloadFa.RuleName);
    }

    [Fact]
    public void AllEnabledAlertTypes_DisplayedInConfiguredRules()
    {
        _alertService.CurrentConfig = new AlertConfig(
            HighDownloadSpeedEnabled: true,
            HighDownloadSpeedThresholdBytesPerSecond: 50 * 1024 * 1024,
            HighUploadSpeedEnabled: true,
            HighUploadSpeedThresholdBytesPerSecond: 20 * 1024 * 1024,
            DailyDownloadLimitEnabled: true,
            DailyDownloadLimitBytes: 50L * 1024 * 1024 * 1024,
            DailyUploadLimitEnabled: true,
            DailyUploadLimitBytes: 20L * 1024 * 1024 * 1024,
            DailyTotalLimitEnabled: true,
            DailyTotalLimitBytes: 100L * 1024 * 1024 * 1024,
            Cooldown: TimeSpan.FromMinutes(5));

        _alertService.RefreshConfig();

        Assert.Equal(5, _vm.ConfiguredRules.Count);
        var types = _vm.ConfiguredRules.Select(r => r.Type).ToList();
        Assert.Contains(AlertType.HighDownloadSpeed, types);
        Assert.Contains(AlertType.HighUploadSpeed, types);
        Assert.Contains(AlertType.DailyDownloadLimit, types);
        Assert.Contains(AlertType.DailyUploadLimit, types);
        Assert.Contains(AlertType.DailyTotalLimit, types);
    }
}