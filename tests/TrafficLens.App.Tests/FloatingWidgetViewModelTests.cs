using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;
using TrafficLens.Core.Models;

namespace TrafficLens.App.Tests;

public sealed class FloatingWidgetViewModelTests : IDisposable
{
    private readonly FakeCollector _collector = new();
    private readonly FakeAdapterProvider _adapters = new();
    private readonly LocalizationService _localization = new();
    private readonly FloatingWidgetViewModel _vm;

    public FloatingWidgetViewModelTests()
    {
        _localization.SetCulture("en-US");
        _vm = new FloatingWidgetViewModel(_collector, _adapters, _localization);
    }

    public void Dispose() => _vm.Dispose();

    private NetworkAdapterInfo Adapter(
        string id,
        NetworkAdapterKind kind,
        bool isUp) =>
        new(id, id, string.Empty, string.Empty, kind, isUp, IsDefault: false);

    [Fact]
    public void Constructor_WithNoSamples_ShowsZeroRates()
    {
        _adapters.SetAdapters(new[] { Adapter("eth0", NetworkAdapterKind.Ethernet, true) });

        Assert.Equal("0 B/s", _vm.DownloadText);
        Assert.Equal("0 B/s", _vm.UploadText);
        Assert.Equal("0 B/s", _vm.TotalText);
    }

    [Fact]
    public void SpeedSample_UpdatesDownloadUploadAndTotalRates()
    {
        _adapters.SetAdapters(new[] { Adapter("eth0", NetworkAdapterKind.Ethernet, true) });

        _collector.RaiseSample(new NetworkSpeedSample("eth0", "Ethernet", 1048576, 524288, DateTime.UtcNow));

        Assert.Equal("1 MB/s", _vm.DownloadText);
        Assert.Equal("512 KB/s", _vm.UploadText);
        Assert.Equal("1.5 MB/s", _vm.TotalText);
    }

    [Fact]
    public void TunnelAdapters_ExcludedFromAggregate()
    {
        _adapters.SetAdapters(new[]
        {
            Adapter("eth0", NetworkAdapterKind.Ethernet, true),
            Adapter("tun0", NetworkAdapterKind.Tunnel, true)
        });

        _collector.RaiseSample(new NetworkSpeedSample("eth0", "Ethernet", 2048, 1024, DateTime.UtcNow));
        _collector.RaiseSample(new NetworkSpeedSample("tun0", "Tunnel", 1048576, 524288, DateTime.UtcNow));

        Assert.Equal("2 KB/s", _vm.DownloadText);
        Assert.Equal("1 KB/s", _vm.UploadText);
    }

    [Fact]
    public void DownAdapters_ExcludedFromAggregate()
    {
        _adapters.SetAdapters(new[] { Adapter("eth0", NetworkAdapterKind.Ethernet, false) });

        _collector.RaiseSample(new NetworkSpeedSample("eth0", "Ethernet", 1048576, 524288, DateTime.UtcNow));

        Assert.Equal("0 B/s", _vm.DownloadText);
        Assert.Equal("0 B/s", _vm.UploadText);
    }

    [Fact]
    public void LocalizedLabels_RefreshOnCultureChange()
    {
        Assert.Equal("Download", _vm.DownloadLabel);
        Assert.Equal("Upload", _vm.UploadLabel);
        Assert.Equal("Total", _vm.TotalLabel);
        Assert.Equal("Floating Widget", _vm.WidgetTitleLabel);

        _localization.SetCulture("fa-IR");

        Assert.NotEqual("Download", _vm.DownloadLabel);
        Assert.NotEqual("Floating Widget", _vm.WidgetTitleLabel);
    }

    [Fact]
    public void CultureChange_ReformatsRatesUsingNewCulture()
    {
        _adapters.SetAdapters(new[] { Adapter("eth0", NetworkAdapterKind.Ethernet, true) });
        _collector.RaiseSample(new NetworkSpeedSample("eth0", "Ethernet", 1536, 1024, DateTime.UtcNow));

        Assert.Equal("1.5 KB/s", _vm.DownloadText);

        _localization.SetCulture("fa-IR");

        Assert.NotEqual("1.5 KB/s", _vm.DownloadText);
    }

    [Fact]
    public void PinIcon_ReflectsPinnedState()
    {
        Assert.True(_vm.IsPinned);
        Assert.Equal("📌", _vm.PinIcon);

        _vm.TogglePinCommand.Execute(null);

        Assert.False(_vm.IsPinned);
        Assert.Equal("📍", _vm.PinIcon);
    }

    [Fact]
    public void AlwaysOnTopLabel_IsLocalized_AndRefreshesOnCultureChange()
    {
        Assert.Equal("Always on Top", _vm.AlwaysOnTopLabel);

        _localization.SetCulture("fa-IR");

        Assert.NotEqual("Always on Top", _vm.AlwaysOnTopLabel);
        Assert.DoesNotContain("[", _vm.AlwaysOnTopLabel);
    }

    [Fact]
    public void Dispose_UnsubscribesEvents_NoCrashOnSubsequentSamples()
    {
        _vm.Dispose();

        _collector.RaiseSample(new NetworkSpeedSample("eth0", "Ethernet", 1000, 500, DateTime.UtcNow));
        _adapters.SetAdapters(new[] { Adapter("eth0", NetworkAdapterKind.Ethernet, true) });
        _localization.SetCulture("fa-IR");
    }
}