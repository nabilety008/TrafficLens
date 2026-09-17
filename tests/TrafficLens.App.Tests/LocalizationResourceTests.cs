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
        "SortNameLabel",
        "ConnectionsLabel",
        "ProtocolLabel",
        "StateLabel",
        "LocalEndpointLabel",
        "RemoteEndpointLabel",
        "ShowLabel",
        "AddressFamilyLabel",
        "SearchConnectionsPlaceholder",
        "NoActiveConnectionsLabel",
        "ConnectionErrorLabel",
        "UnknownProcessLabel",
        "AllLabel",
        "EstablishedLabel",
        "ListeningLabel",
        "TcpLabel",
        "UdpLabel",
        "Ipv4Label",
        "Ipv6Label",
        "StateClosedLabel",
        "StateListenLabel",
        "StateSynSentLabel",
        "StateSynReceivedLabel",
        "StateEstablishedLabel",
        "StateFinWait1Label",
        "StateFinWait2Label",
        "StateCloseWaitLabel",
        "StateClosingLabel",
        "StateLastAckLabel",
        "StateTimeWaitLabel",
        "SortDefaultLabel",
        "SortProcessLabel",
        "SortPidLabel",
        "SortProtocolLabel",
        "SortStateLabel",
        "SortLocalLabel",
        "SortRemoteLabel",
        "HistoryLabel",
        "HistoryDailyTrafficLabel",
        "TodayLabel",
        "YesterdayLabel",
        "Last7DaysLabel",
        "Last30DaysLabel",
        "LifetimeLabel",
        "HistoryNoDataLabel",
        "HistoryUnavailableLabel",
        "FloatingWidgetLabel",
        "AlwaysOnTopLabel",
        "ShowFloatingWidgetLabel",
        "HideFloatingWidgetLabel",
        "OpenTrafficLensLabel",
        "ExitLabel",
        "MinimizeToTrayLabel",
        "CloseToTrayLabel",
        "TrayCloseNoticeBalloon",
        "AlertsNavLabel",
        "AlertsTitleLabel",
        "AlertsNoAlertsLabel",
        "AlertsCountFormat",
        "AlertTitle",
        "AlertTypeHighDownloadSpeed",
        "AlertTypeHighUploadSpeed",
        "AlertTypeDailyDownloadLimit",
        "AlertTypeDailyUploadLimit",
        "AlertTypeDailyTotalLimit",
        "AlertTypeUnknown",
        "AlertMsgHighDownloadSpeed",
        "AlertMsgHighUploadSpeed",
        "AlertMsgDailyDownloadLimit",
        "AlertMsgDailyUploadLimit",
        "AlertMsgDailyTotalLimit",
        "SettingsNavLabel",
        "SettingsTitleLabel",
        "GeneralLabel",
        "LanguageLabel",
        "StartWithWindowsLabel",
        "StartMinimizedLabel",
        "SystemTrayLabel",
        "WidgetLabel",
        "EnableFloatingWidgetLabel",
        "AlertsLabel",
        "CooldownLabel",
        "MinutesLabel",
        "CooldownRangeLabel",
        "SaveLabel",
        "ChangesSavedLabel",
        "ResetToDefaultsLabel",
        "ResetAreYouSureLabel",
        "InvalidValueLabel",
        "PermissionDeniedDetailLabel",
        "MonitoringFailedDetailLabel",
        "ConnectionsErrorDetailLabel",
        "HistoryErrorDetailLabel",
        "AboutNavLabel",
        "AboutTitleLabel",
        "AboutDescriptionLabel",
        "ProductNameLabel",
        "VersionLabel",
        "RuntimeLabel",
        "OsLabel",
        "CultureLabel",
        "DataPathLabel",
        "SettingsPathLabel",
        "LogsPathLabel",
        "DiagnosticsHeaderLabel",
        "CopyDiagnosticsLabel",
        "DiagnosticsCopiedLabel",
        "OpenLogFolderLabel"
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
        Assert.NotEqual(
            service.GetString("HistoryLabel", "en-US"),
            service.GetString("HistoryLabel", "fa-IR"));
    }
}