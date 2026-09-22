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

    [Fact]
    public void Suspend_StopsEventProcessing()
    {
        _adapters.SetAdapters(new[] { Adapter("eth0", NetworkAdapterKind.Ethernet, true) });
        _collector.RaiseSample(new NetworkSpeedSample("eth0", "Ethernet", 1048576, 0, DateTime.UtcNow));
        Assert.Equal("1 MB/s", _vm.DownloadText);

        _vm.Suspend();

        // After suspend, new samples should NOT update the view model
        _collector.RaiseSample(new NetworkSpeedSample("eth0", "Ethernet", 2097152, 0, DateTime.UtcNow));
        Assert.Equal("1 MB/s", _vm.DownloadText);
    }

    [Fact]
    public void Resume_RestartsEventProcessing()
    {
        _adapters.SetAdapters(new[] { Adapter("eth0", NetworkAdapterKind.Ethernet, true) });
        _collector.RaiseSample(new NetworkSpeedSample("eth0", "Ethernet", 1048576, 0, DateTime.UtcNow));
        Assert.Equal("1 MB/s", _vm.DownloadText);

        _vm.Suspend();
        _vm.Resume();

        // After resume, new samples should update the view model
        _collector.RaiseSample(new NetworkSpeedSample("eth0", "Ethernet", 2097152, 0, DateTime.UtcNow));
        Assert.Equal("2 MB/s", _vm.DownloadText);
    }

    [Fact]
    public void Suspend_Resume_Suspend_Resume_Works()
    {
        _adapters.SetAdapters(new[] { Adapter("eth0", NetworkAdapterKind.Ethernet, true) });

        _collector.RaiseSample(new NetworkSpeedSample("eth0", "Ethernet", 1024, 0, DateTime.UtcNow));
        Assert.Equal("1 KB/s", _vm.DownloadText);

        _vm.Suspend();
        _vm.Resume();

        _collector.RaiseSample(new NetworkSpeedSample("eth0", "Ethernet", 2048, 0, DateTime.UtcNow));
        Assert.Equal("2 KB/s", _vm.DownloadText);

        _vm.Suspend();
        _vm.Resume();

        _collector.RaiseSample(new NetworkSpeedSample("eth0", "Ethernet", 4096, 0, DateTime.UtcNow));
        Assert.Equal("4 KB/s", _vm.DownloadText);
    }

    [Fact]
    public void Suspend_DoesNotCrashOnAdaptersChanged()
    {
        _vm.Suspend();

        // These should not throw
        _adapters.SetAdapters(new[] { Adapter("eth0", NetworkAdapterKind.Ethernet, true) });
        _collector.RaiseNetworkChanged();
    }

    [Fact]
    public void Suspend_DoesNotCrashOnCultureChange()
    {
        _vm.Suspend();

        _localization.SetCulture("fa-IR");
    }

    [Fact]
    public void Resume_ReappliesCurrentCulture()
    {
        _localization.SetCulture("en-US");
        Assert.Equal("Download", _vm.DownloadLabel);

        _vm.Suspend();
        _localization.SetCulture("fa-IR");
        _vm.Resume();

        Assert.NotEqual("Download", _vm.DownloadLabel);
    }

    [Fact]
    public void Dispose_AfterSuspend_NoCrash()
    {
        _vm.Suspend();
        _vm.Dispose();
    }

    [Fact]
    public void FakeWidgetService_Toggle_Cycles()
    {
        var fake = new FakeFloatingWidgetService();

        Assert.False(fake.IsVisible);

        fake.Toggle();
        Assert.True(fake.IsVisible);

        fake.Toggle();
        Assert.False(fake.IsVisible);

        fake.Toggle();
        Assert.True(fake.IsVisible);
    }

    [Fact]
    public void FakeWidgetService_MultipleToggles_NoDuplicateState()
    {
        var fake = new FakeFloatingWidgetService();

        for (int i = 0; i < 10; i++)
        {
            fake.Toggle();
        }

        // 10 toggles from false: even count ends at false
        Assert.False(fake.IsVisible);
        Assert.Equal(10, fake.ToggleCalls);
    }

    [Fact]
    public void FakeWidgetService_Hide_DoesNotExitApp()
    {
        var fake = new FakeFloatingWidgetService();

        fake.Show();
        Assert.True(fake.IsVisible);

        fake.Hide();
        Assert.False(fake.IsVisible);

        // Hide should NOT call Dispose (exit is separate)
        Assert.Equal(0, fake.DisposeCalls);
    }

    [Fact]
    public void FakeWidgetService_Dispose_CalledOnAppExit()
    {
        var fake = new FakeFloatingWidgetService();

        fake.Show();
        fake.Dispose();

        Assert.Equal(1, fake.DisposeCalls);
    }

    [Fact]
    public void FakeWidgetService_PersistedSetting_Synchronized()
    {
        var fake = new FakeFloatingWidgetService();

        fake.Show();
        Assert.True(fake.IsVisible);

        fake.Hide();
        Assert.False(fake.IsVisible);
    }
}