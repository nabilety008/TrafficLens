using System.Net;
using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;
using TrafficLens.Core.Models;
using TrafficLens.Core.Selection;

namespace TrafficLens.App.Tests;

public sealed class ConnectionsViewModelTests : IDisposable
{
    private readonly FakeConnectionProvider _provider = new();
    private readonly LocalizationService _localization = new();
    private readonly ProcessIconResolver _icons = new();
    private readonly FakeSettingsService _settings = new();
    private readonly DnsResolverService _dnsResolver;
    private readonly ConnectionsViewModel _vm;

    public ConnectionsViewModelTests()
    {
        _localization.SetCulture("en-US");
        _dnsResolver = new DnsResolverService(Microsoft.Extensions.Logging.Abstractions.NullLogger<DnsResolverService>.Instance);
        _vm = new ConnectionsViewModel(_provider, _localization, _icons, _settings, _dnsResolver);
        _vm.SetActive(true);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _dnsResolver.Dispose();
    }

    private static ConnectionInfo Tcp(
        string local,
        int localPort,
        string remote,
        int remotePort,
        ConnectionState state,
        int pid,
        string name) =>
        new(pid, name, ConnectionProtocol.Tcp, ConnectionAddressFamily.Ipv4,
            IPAddress.Parse(local), localPort, IPAddress.Parse(remote), remotePort, state);

    private static ConnectionInfo Udp(string local, int localPort, int pid, string name) =>
        new(pid, name, ConnectionProtocol.Udp, ConnectionAddressFamily.Ipv4,
            IPAddress.Parse(local), localPort, null, null, ConnectionState.Unknown);

    private static ConnectionInfo Udp(
        string local,
        int localPort,
        string? remote,
        int? remotePort,
        int pid,
        string? name) =>
        new(pid, name, ConnectionProtocol.Udp, ConnectionAddressFamily.Ipv4,
            IPAddress.Parse(local), localPort,
            remote is null ? null : IPAddress.Parse(remote), remotePort, ConnectionState.Unknown);

    private static ConnectionInfo UdpIpv6(int pid, string name) =>
        new(pid, name, ConnectionProtocol.Udp, ConnectionAddressFamily.Ipv6,
            IPAddress.IPv6Loopback, 5353, null, null, ConnectionState.Unknown);

    [Fact]
    public void Constructor_EmptyProvider_ShowsEmptyState()
    {
        Assert.Empty(_vm.Connections);
        Assert.True(_vm.IsEmpty);
        Assert.False(_vm.HasError);
    }

    [Fact]
    public void Publish_CreatesOneRowPerKey()
    {
        _provider.Publish(
            Tcp("10.0.0.1", 100, "8.8.8.8", 53, ConnectionState.Established, 1, "chrome"),
            Tcp("10.0.0.1", 101, "8.8.4.4", 53, ConnectionState.Listen, 2, "svc"),
            Udp("10.0.0.1", 5353, 3, "go"));

        Assert.Equal(3, _vm.Connections.Count);
        Assert.False(_vm.IsEmpty);
        Assert.Equal(3, _vm.Rows.Count());
    }

    [Fact]
    public void RepublishedConnection_UpdatesInPlace_NoDuplicate()
    {
        _provider.Publish(Tcp("10.0.0.1", 100, "8.8.8.8", 53, ConnectionState.Established, 1, "chrome"));
        _provider.Publish(Tcp("10.0.0.1", 100, "8.8.8.8", 53, ConnectionState.CloseWait, 1, "chrome"));

        var row = Assert.Single(_vm.Connections);
        Assert.Equal("CLOSE-WAIT", row.StateText);
        Assert.Single(_vm.Rows);
    }

    [Fact]
    public void GoneConnection_IsRemoved()
    {
        _provider.Publish(
            Tcp("10.0.0.1", 100, "8.8.8.8", 53, ConnectionState.Established, 1, "chrome"),
            Tcp("10.0.0.1", 200, "8.8.8.8", 53, ConnectionState.Established, 2, "svc"));

        _provider.Publish(Tcp("10.0.0.1", 100, "8.8.8.8", 53, ConnectionState.Established, 1, "chrome"));

        var row = Assert.Single(_vm.Connections);
        Assert.Equal("chrome", row.Name);
    }

    [Fact]
    public void Search_FiltersByNamePidAndEndpoint()
    {
        var all = new[]
        {
            Tcp("10.0.0.1", 443, "8.8.8.8", 53, ConnectionState.Established, 5000, "chrome"),
            Tcp("10.0.0.2", 80, "8.8.4.4", 443, ConnectionState.Listen, 90, "nginx")
        };
        _provider.Publish(all);

        _vm.SearchText = "chrome";
        var row = Assert.Single(_vm.Connections);
        Assert.Equal("chrome", row.Name);

        _vm.SearchText = "5000";
        Assert.Single(_vm.Connections);

        _vm.SearchText = "8.8.4.4";
        Assert.Equal("nginx", Assert.Single(_vm.Connections).Name);

        _vm.SearchText = "no-match";
        Assert.Empty(_vm.Connections);
        Assert.True(_vm.IsEmpty);
    }

    [Fact]
    public void Filter_Established_ShowsOnlyActiveConnections()
    {
        _provider.Publish(
            Tcp("1", 1, "2", 2, ConnectionState.Established, 1, "a"),
            Tcp("1", 2, "3", 3, ConnectionState.Listen, 2, "b"),
            Udp("1", 3, 3, "c"));

        _vm.Filter = ConnectionFilter.Established;

        var row = Assert.Single(_vm.Connections);
        Assert.Equal("a", row.Name);
    }

    [Fact]
    public void Filter_TcpUdp_RespectsProtocol()
    {
        _provider.Publish(
            Tcp("1", 1, "2", 2, ConnectionState.Established, 1, "a"),
            Udp("1", 2, 3, "c"));

        _vm.Filter = ConnectionFilter.Udp;
        Assert.Equal("c", Assert.Single(_vm.Connections).Name);
    }

    [Fact]
    public void FamilyFilter_Ipv6_OnlyIpv6Rows()
    {
        _provider.Publish(
            Tcp("1", 1, "2", 2, ConnectionState.Established, 1, "a"),
            UdpIpv6(3, "v6udp"));

        _vm.FamilyFilter = ConnectionFilter.Ipv6;

        Assert.Equal("v6udp", Assert.Single(_vm.Connections).Name);
    }

    [Fact]
    public void Sort_ProcessIdOrdersRows()
    {
        _provider.Publish(
            Tcp("1", 1, "2", 2, ConnectionState.Established, 200, "z"),
            Tcp("1", 2, "2", 2, ConnectionState.Established, 10, "a"));

        _vm.SortKey = ConnectionSortKey.ProcessId;

        Assert.Equal("a", _vm.Connections[0].Name);
        Assert.Equal("z", _vm.Connections[1].Name);
    }

    [Fact]
    public void UdpRow_StateColumnShowsEmDash()
    {
        _provider.Publish(Udp("10.0.0.1", 5353, 3, "go"));

        Assert.Equal("—", Assert.Single(_vm.Connections).StateText);
        Assert.Equal(string.Empty, Assert.Single(_vm.Connections).RemoteText);
    }

    [Fact]
    public void ListeningRow_HasEmptyRemoteEndpoint()
    {
        _provider.Publish(Tcp("127.0.0.1", 18888, "0.0.0.0", 0, ConnectionState.Listen, 42, "listener"));

        var row = Assert.Single(_vm.Connections);
        Assert.Equal("127.0.0.1:18888", row.LocalText);
        Assert.Equal(string.Empty, row.RemoteText);
    }

    [Fact]
    public void Ipv6Row_LocalEndpointIsFormattedWithBrackets_LeftToRight()
    {
        _provider.Publish(UdpIpv6(3, "v6udp"));

        Assert.Equal("[::1]:5353", Assert.Single(_vm.Connections).LocalText);
    }

    [Fact]
    public void ProviderError_SurfacesAsLocalizedBanner_WithoutClearingRows()
    {
        _provider.Publish(Tcp("1", 1, "2", 2, ConnectionState.Established, 1, "a"));

        _provider.SetError("boom");

        Assert.True(_vm.HasError);
        Assert.Equal(_localization["ConnectionsErrorDetailLabel"], _vm.ErrorDetail);
        Assert.DoesNotContain("boom", _vm.ErrorDetail);
        Assert.Single(_vm.Connections);
    }

    [Fact]
    public void CultureSwitch_LocalizesStateAndProtocolCells()
    {
        _provider.Publish(Tcp("1", 1, "2", 2, ConnectionState.Established, 1, "app"));

        _localization.SetCulture("fa-IR");

        var row = Assert.Single(_vm.Connections);
        Assert.Equal("برقرار", row.StateText);
        Assert.Equal("TCP", row.ProtocolText);
    }

    [Fact]
    public void UnknownProcess_DisplaysLocalizedFallback()
    {
        var unknown = new ConnectionInfo(
            9, null, ConnectionProtocol.Tcp, ConnectionAddressFamily.Ipv4,
            IPAddress.Parse("1.1.1.1"), 1, IPAddress.Parse("2.2.2.2"), 2,
            ConnectionState.Established);
        _provider.Publish(unknown);

        Assert.Equal("Unknown Process", Assert.Single(_vm.Connections).Name);

        _localization.SetCulture("fa-IR");

        Assert.Equal("فرآیند ناشناخته", Assert.Single(_vm.Connections).Name);
    }

    [Fact]
    public void Filter_HideListeners_HidesTcpListenersAndUnconnectedUdp()
    {
        _provider.Publish(
            Tcp("1", 1, "2", 2, ConnectionState.Established, 1, "a"),
            Tcp("1", 1, "0.0.0.0", 0, ConnectionState.Listen, 1, "b"),
            Udp("1", 1, 1, "c"),  // UDP no remote
            Udp("1", 2, "2", 2, 2, "d")); // UDP with remote

        _vm.Filter = ConnectionFilter.HideListeners;

        Assert.Equal(2, _vm.Connections.Count);
        Assert.Contains(_vm.Connections, r => r.Name == "a");
        Assert.Contains(_vm.Connections, r => r.Name == "d");
    }

    [Fact]
    public void HideListeners_Setting_PersistsAndRestores()
    {
        _vm.HideListeners = true;
        _vm.HideListeners = false;
        Assert.False(_vm.HideListeners);

        var saveCalls = _settings.SaveCalls;
        _vm.HideListeners = true;
        Assert.True(_vm.HideListeners);
        Assert.Equal(saveCalls + 1, _settings.SaveCalls);
    }

    [Fact]
    public void EnableReverseDns_DefaultFalse_PersistsAndRestores()
    {
        Assert.False(_vm.EnableReverseDns);

        var saveCalls = _settings.SaveCalls;
        _vm.EnableReverseDns = true;
        Assert.True(_vm.EnableReverseDns);
        Assert.Equal(saveCalls + 1, _settings.SaveCalls);

        _vm.EnableReverseDns = false;
        Assert.False(_vm.EnableReverseDns);
        Assert.Equal(saveCalls + 2, _settings.SaveCalls);
    }

    [Fact]
    public void CultureSwitch_UpdatesHideListenersLabel()
    {
        var initialLabel = _vm.HideListenersLabel;
        Assert.Equal("Hide listeners", initialLabel);

        _localization.SetCulture("fa-IR");
        Assert.Equal("پنهان کردن شنونده‌ها", _vm.HideListenersLabel);

        _localization.SetCulture("en-US");
        Assert.Equal("Hide listeners", _vm.HideListenersLabel);
    }

    [Fact]
    public void CultureSwitch_UpdatesEnableReverseDnsLabel()
    {
        var initialLabel = _vm.EnableReverseDnsLabel;
        Assert.Equal("Resolve remote hostnames (reverse DNS)", initialLabel);

        _localization.SetCulture("fa-IR");
        Assert.Equal("بررسی نام میزبان‌ها (DNS معکوس)", _vm.EnableReverseDnsLabel);

        _localization.SetCulture("en-US");
        Assert.Equal("Resolve remote hostnames (reverse DNS)", _vm.EnableReverseDnsLabel);
    }
}