using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;
using TrafficLens.Core.Graph;
using TrafficLens.Core.Models;

namespace TrafficLens.App.Tests;

public sealed class DashboardGraphTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeCollector _collector = new();
    private readonly FakeAdapterProvider _provider = new();
    private readonly LocalizationService _localization = new();
    private readonly DashboardViewModel _vm;

    public DashboardGraphTests()
    {
        _localization.SetCulture("en-US");
        SetupConnected();
        _vm = new DashboardViewModel(_collector, _provider, _localization);
    }

    public void Dispose() => _vm.Dispose();

    [Fact]
    public void Graph_AppendsAggregateSamples_IntoBuffer()
    {
        Assert.Empty(_vm.GraphPoints);
        Assert.Equal(2048L, _vm.GraphScaleMax);

        _collector.RaiseSample(Speed("wifi", "Wi-Fi", T0.AddSeconds(1), 200_000, 50_000));
        Assert.Single(_vm.GraphPoints);
        Assert.Equal(200_000, _vm.GraphPoints[0].DownloadBytesPerSecond);
        Assert.Equal(50_000, _vm.GraphPoints[0].UploadBytesPerSecond);

        _collector.RaiseSample(Speed("wifi", "Wi-Fi", T0.AddSeconds(2), 300_000, 80_000));
        Assert.Equal(2, _vm.GraphPoints.Count);
        Assert.Equal(300_000, _vm.GraphPoints[^1].DownloadBytesPerSecond);
    }

    [Fact]
    public void Graph_DeduplicatesSameTimestampPerPoll()
    {
        _provider.SetAdapters(new[] { Adapter("wifi", "Wi-Fi", true, true) });

        _collector.RaiseSample(Speed("wifi", "Wi-Fi", T0.AddSeconds(1), 100_000, 10_000));
        _collector.RaiseSample(Speed("wifi", "Wi-Fi", T0.AddSeconds(1), 100_000, 10_000));
        _collector.RaiseSample(Speed("wifi", "Wi-Fi", T0.AddSeconds(1), 100_000, 10_000));

        Assert.Single(_vm.GraphPoints);

        _collector.RaiseSample(Speed("wifi", "Wi-Fi", T0.AddSeconds(2), 120_000, 12_000));
        Assert.Equal(2, _vm.GraphPoints.Count);
    }

    [Fact]
    public void RangeSwitching_DisplaysCorrespondingSlice_WithoutClearing()
    {
        _provider.SetAdapters(new[] { Adapter("wifi", "Wi-Fi", true, true) });

        for (var i = 0; i < 200; i++)
        {
            _collector.RaiseSample(Speed("wifi", "Wi-Fi", T0.AddSeconds(i), 1000 + i, i));
        }

        Assert.Equal(61, _vm.GraphPoints.Count);

        _vm.SelectGraphRangeCommand.Execute("30");
        Assert.Equal(31, _vm.GraphPoints.Count);

        _vm.SelectGraphRangeCommand.Execute("60");
        Assert.Equal(61, _vm.GraphPoints.Count);

        _vm.SelectGraphRangeCommand.Execute("300");
        Assert.Equal(200, _vm.GraphPoints.Count);
    }

    [Fact]
    public void NoConnection_KeepsHistory_AndDoesNotCrash()
    {
        _provider.SetAdapters(new[] { Adapter("wifi", "Wi-Fi", true, true) });
        _collector.RaiseSample(Speed("wifi", "Wi-Fi", T0.AddSeconds(1), 500_000, 100_000));
        Assert.Single(_vm.GraphPoints);

        _provider.SetAdapters(new[] { Adapter("wifi", "Wi-Fi", isUp: false, isDefault: true) });
        _collector.RaiseNetworkChanged();

        Assert.Single(_vm.GraphPoints);
        Assert.False(_vm.HasConnection);
        Assert.True(_vm.GraphScaleMax >= AdaptiveGraphScale.FloorBytesPerSecond);

        _provider.SetAdapters(new[] { Adapter("wifi", "Wi-Fi", isUp: true, isDefault: true) });
        _collector.RaiseSample(Speed("wifi", "Wi-Fi", T0.AddSeconds(2), 500_000, 100_000));
        Assert.Equal(2, _vm.GraphPoints.Count);
    }

    [Fact]
    public void ZeroTraffic_ScaleAtFloor_NoDivideByZero()
    {
        _provider.SetAdapters(new[] { Adapter("wifi", "Wi-Fi", true, true) });
        for (var i = 0; i < 60; i++)
        {
            _collector.RaiseSample(Speed("wifi", "Wi-Fi", T0.AddSeconds(i), 0, 0));
        }

        Assert.Equal(60, _vm.GraphPoints.Count);
        Assert.True(_vm.GraphScaleMax >= AdaptiveGraphScale.FloorBytesPerSecond);
    }

    [Fact]
    public void LargeValues_ScaleAdaptsAbovePeak()
    {
        _provider.SetAdapters(new[] { Adapter("wifi", "Wi-Fi", true, true) });
        _collector.RaiseSample(Speed("wifi", "Wi-Fi", T0.AddSeconds(1), 1_000_000_000, 200_000_000));
        Assert.True(_vm.GraphScaleMax >= 1_000_000_000);
    }

    private void SetupConnected()
    {
        _provider.SetAdapters(new[]
        {
            Adapter("wifi", "Wi-Fi", true, true),
            Adapter("tun", "OpenVPN DCO", true, false)
        });
    }

    private static NetworkAdapterInfo Adapter(string id, string name, bool isUp, bool isDefault)
    {
        return new NetworkAdapterInfo(id, name, $"desc-{name}", string.Empty,
            isDefault ? NetworkAdapterKind.Wireless : NetworkAdapterKind.Tunnel,
            isUp, isDefault);
    }

    private static NetworkSpeedSample Speed(
        string id,
        string name,
        DateTime timestamp,
        long download,
        long upload) =>
        new(id, name, download, upload, timestamp);
}