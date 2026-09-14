using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;
using TrafficLens.Core.Models;

namespace TrafficLens.App.Tests;

public sealed class DashboardViewModelTests : IDisposable
{
    private readonly FakeCollector _collector = new();
    private readonly FakeAdapterProvider _provider = new();
    private readonly LocalizationService _localization = new();
    private readonly DashboardViewModel _vm;

    public DashboardViewModelTests()
    {
        _localization.SetCulture("en-US");
        _vm = new DashboardViewModel(_collector, _provider, _localization);
    }

    public void Dispose() => _vm.Dispose();

    [Fact]
    public void Cards_ReflectSystemAggregate_AndExcludeTunnelDoubleCounting()
    {
        _provider.SetAdapters(new[]
        {
            Adapter("wifi", "Wi-Fi", NetworkAdapterKind.Wireless, isUp: true, isDefault: true),
            Adapter("tun", "OpenVPN DCO", NetworkAdapterKind.Tunnel, isUp: true)
        });

        _collector.SetSamples(
            Sample("wifi", "Wi-Fi", 3_000_000, 200_000),
            Sample("tun", "OpenVPN DCO", 1_000_000, 300_000));

        Assert.Equal(3_000_000, _vm.DownloadBytesPerSecond);
        Assert.Equal(200_000, _vm.UploadBytesPerSecond);
        Assert.Equal(3_200_000, _vm.TotalBytesPerSecond);
        Assert.Equal("2.86 MB/s", _vm.DownloadText);
        Assert.Equal("195.31 KB/s", _vm.UploadText);
        Assert.Equal("25.6 Mbps", _vm.TotalMbpsText);
        Assert.True(_vm.HasConnection);
    }

    [Fact]
    public void AdapterList_ShowsPerAdapterRates_IncludingTunnels()
    {
        _provider.SetAdapters(new[]
        {
            Adapter("wifi", "Wi-Fi", NetworkAdapterKind.Wireless, isUp: true, isDefault: true),
            Adapter("tun", "OpenVPN DCO", NetworkAdapterKind.Tunnel, isUp: true)
        });

        _collector.SetSamples(
            Sample("wifi", "Wi-Fi", 3_000_000, 200_000),
            Sample("tun", "OpenVPN DCO", 1_000_000, 300_000));

        Assert.Equal(2, _vm.Adapters.Count);

        var wifi = Assert.Single(_vm.Adapters, a => a.Id == "wifi");
        Assert.Equal("2.86 MB/s", wifi.DownloadRateText);
        Assert.Equal("195.31 KB/s", wifi.UploadRateText);
        Assert.Equal("Connected", wifi.StatusText);

        var tunnel = Assert.Single(_vm.Adapters, a => a.Id == "tun");
        Assert.Equal("976.56 KB/s", tunnel.DownloadRateText);
        Assert.Equal("292.97 KB/s", tunnel.UploadRateText);
        Assert.Equal("Connected", tunnel.StatusText);
    }

    [Fact]
    public void NoActiveConnection_WhenAllAdaptersDown_ShowsZeros()
    {
        _provider.SetAdapters(new[]
        {
            Adapter("wifi", "Wi-Fi", NetworkAdapterKind.Wireless, isUp: false)
        });

        _collector.SetSamples();

        Assert.False(_vm.HasConnection);
        Assert.Equal(0, _vm.DownloadBytesPerSecond);
        Assert.Equal(0, _vm.UploadBytesPerSecond);
        Assert.Equal(0, _vm.TotalBytesPerSecond);
        Assert.Equal("0 B/s", _vm.TotalText);
        Assert.Equal("0 Mbps", _vm.TotalMbpsText);
        Assert.Equal(_vm.NoActiveConnectionText, _vm.ActiveAdapterName);
        Assert.Equal("Disconnected", Assert.Single(_vm.Adapters).StatusText);
    }

    [Fact]
    public void ReconnectAfterDisconnect_UpdatesState_WithoutDuplicates()
    {
        _provider.SetAdapters(new[]
        {
            Adapter("wifi", "Wi-Fi", NetworkAdapterKind.Wireless, isUp: false)
        });
        _collector.SetSamples();

        Assert.False(_vm.HasConnection);
        Assert.Equal(_vm.NoActiveConnectionText, _vm.ActiveAdapterName);

        _provider.SetAdapters(new[]
        {
            Adapter("wifi", "Wi-Fi", NetworkAdapterKind.Wireless, isUp: true, isDefault: true)
        });
        _collector.SetSamples(Sample("wifi", "Wi-Fi", 1_500_000, 60_000));

        Assert.True(_vm.HasConnection);
        Assert.Single(_vm.Adapters);
        Assert.Equal("Wi-Fi", _vm.ActiveAdapterName);
        Assert.Equal("Connected", _vm.ActiveAdapterStatusText);
        Assert.Equal("1.43 MB/s", _vm.DownloadText);
        Assert.Equal("58.59 KB/s", _vm.UploadText);
    }

    [Fact]
    public void VpnOnlyHost_KeepsTunnelRatesInList_AndHonestZeroTotal()
    {
        _provider.SetAdapters(new[]
        {
            Adapter("tun", "OpenVPN DCO", NetworkAdapterKind.Tunnel, isUp: true, isDefault: true)
        });

        _collector.SetSamples(Sample("tun", "OpenVPN DCO", 4_000_000, 100_000));

        Assert.True(_vm.HasConnection);
        Assert.Equal(0, _vm.TotalBytesPerSecond);
        Assert.Equal("0 B/s", _vm.TotalText);
        Assert.Equal("OpenVPN DCO", _vm.ActiveAdapterName);

        var tunnel = Assert.Single(_vm.Adapters);
        Assert.Equal("3.81 MB/s", tunnel.DownloadRateText);
        Assert.Equal("97.66 KB/s", tunnel.UploadRateText);
    }

    [Fact]
    public void CultureSwitch_ReformatsRatesAndRelabels()
    {
        _provider.SetAdapters(new[]
        {
            Adapter("wifi", "Wi-Fi", NetworkAdapterKind.Wireless, isUp: true, isDefault: true)
        });
        _collector.SetSamples(Sample("wifi", "Wi-Fi", 3_240_000, 100_000));

        _localization.SetCulture("fa-IR");

        Assert.Equal("دانلود", _vm.DownloadLabel);
        Assert.Equal("مجموع", _vm.TotalLabel);
        Assert.Equal("آداپتورهای شبکه", _vm.NetworkAdaptersLabel);
        Assert.Contains("٫", _vm.DownloadMbpsText);
        Assert.Equal("Wi-Fi", _vm.ActiveAdapterName);
        Assert.Equal("متصل", _vm.ActiveAdapterStatusText);
    }

    private static NetworkAdapterInfo Adapter(
        string id,
        string name,
        NetworkAdapterKind kind,
        bool isUp,
        bool isDefault = false)
    {
        return new NetworkAdapterInfo(id, name, "desc-" + name, string.Empty, kind, isUp, isDefault)
        {
            IpAddress = isUp ? "10.0.0.5" : null,
            LinkSpeedBitsPerSecond = isUp ? 1_000_000_000L : null
        };
    }

    private static NetworkSpeedSample Sample(string id, string name, long download, long upload) =>
        new(id, name, download, upload, DateTime.UtcNow);
}