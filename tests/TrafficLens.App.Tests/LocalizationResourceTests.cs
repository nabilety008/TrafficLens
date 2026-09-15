using TrafficLens.App.Services;

namespace TrafficLens.App.Tests;

public class LocalizationResourceTests
{
    private static readonly string[] RequiredKeys =
    {
        "DashboardLabel",
        "DownloadLabel",
        "UploadLabel",
        "TotalRateLabel",
        "ActiveAdapterLabel",
        "NetworkAdaptersLabel",
        "ConnectedLabel",
        "DisconnectedLabel",
        "StatusLabel",
        "NoActiveConnection",
        "PlaceholderDashboardText",
        "WindowTitle",
        "StatusReady",
        "KindEthernet",
        "KindWireless",
        "KindTunnel",
        "KindVirtual",
        "KindUnknown",
        "GraphLiveTrafficLabel",
        "GraphLast30SecondsLabel",
        "GraphLast1MinuteLabel",
        "GraphLast5MinutesLabel",
        "GraphNowLabel",
        "GraphDownloadSeriesLabel",
        "GraphUploadSeriesLabel",
        "ApplicationsLabel",
        "TopConsumerNowLabel",
        "TopDownloadLabel",
        "TopUploadLabel",
        "PidLabel",
        "TotalTransferredLabel",
        "SearchPlaceholder",
        "SortByLabel",
        "NoActiveTrafficLabel",
        "StatusStartingLabel",
        "MonitoringStoppedLabel",
        "MonitoringFailedLabel",
        "AdminPermissionRequiredLabel",
        "RunningLabel",
        "ExitedLabel",
        "RestartAsAdministratorLabel",
        "StartMonitoringLabel",
        "CurrentSpeedLabel",
        "ProcessLabel",
        "SortTotalRateLabel",
        "SortDownloadRateLabel",
        "SortUploadRateLabel",
        "SortDownloadedLabel",
        "SortUploadedLabel",
        "SortNameLabel"
    };

    [Fact]
    public void RequiredDashboardKeys_ExistInEnglishAndPersian()
    {
        var service = new LocalizationService();

        foreach (var key in RequiredKeys)
        {
            var english = service.GetString(key, "en-US");
            var persian = service.GetString(key, "fa-IR");

            Assert.False(string.IsNullOrWhiteSpace(english), $"en '{key}' empty");
            Assert.False(string.IsNullOrWhiteSpace(persian), $"fa '{key}' empty");
            Assert.False(english.StartsWith("["), $"en '{key}' missing");
            Assert.False(persian.StartsWith("["), $"fa '{key}' missing");
        }
    }

    [Fact]
    public void UserFacingDashboardLabels_DifferBetweenCultures()
    {
        var service = new LocalizationService();

        Assert.NotEqual(
            service.GetString("DashboardLabel", "en-US"),
            service.GetString("DashboardLabel", "fa-IR"));
        Assert.NotEqual(
            service.GetString("NoActiveConnection", "en-US"),
            service.GetString("NoActiveConnection", "fa-IR"));
        Assert.NotEqual(
            service.GetString("ConnectedLabel", "en-US"),
            service.GetString("ConnectedLabel", "fa-IR"));
    }
}