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
        Assert.Equal("No alerts have been triggered.", _vm.NoAlertsText);
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
}