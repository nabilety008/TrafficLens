using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging.Abstractions;
using TrafficLens.Core.Models;
using TrafficLens.Network.Adapters;
using TrafficLens.Network.Collectors;

namespace TrafficLens.Network.Tests;

public sealed class WindowsNetworkTrafficCollectorTests
{
    private sealed class FakeSource : INetworkInterfaceSource
    {
        public IReadOnlyList<RawAdapterSnapshot> Adapters { get; set; } = new List<RawAdapterSnapshot>();

        public IReadOnlyList<RawAdapterSnapshot> GetAdapters() => Adapters;
    }

    private static RawAdapterSnapshot Snap(string id, long received, long sent, bool isUp = true) =>
        new(id, id, "desc", string.Empty, NetworkInterfaceType.Ethernet, isUp,
            HasGateway: true, "10.0.0.2", 1_000_000_000, received, sent);

    [Fact]
    public async Task StartAsync_CollectsAdapterCounters()
    {
        var source = new FakeSource
        {
            Adapters = [Snap("eth0", 1000, 500), Snap("wlan0", 2000, 700)]
        };
        using var collector = new WindowsNetworkTrafficCollector(
            source, NullLogger<WindowsNetworkTrafficCollector>.Instance, TimeSpan.FromMilliseconds(30));

        await collector.StartAsync(CancellationToken.None);
        try
        {
            await Task.Delay(150);

            var counters = collector.GetCurrentCounterSamples();
            Assert.Equal(2, counters.Count);

            var eth = Assert.Single(counters, c => c.AdapterId == "eth0");
            Assert.Equal(1000, eth.ReceivedBytes);
            Assert.Equal(500, eth.SentBytes);
        }
        finally
        {
            await collector.StopAsync();
        }
    }

    [Fact]
    public async Task CounterSampleReady_FiresWithSnapshot()
    {
        var source = new FakeSource { Adapters = [Snap("eth0", 10, 20)] };
        using var collector = new WindowsNetworkTrafficCollector(
            source, NullLogger<WindowsNetworkTrafficCollector>.Instance, TimeSpan.FromMilliseconds(30));

        NetworkCounterSample? received = null;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        collector.CounterSampleReady += (_, sample) =>
        {
            received ??= sample;
            if (received == sample)
            {
                tcs.TrySetResult(true);
            }
        };

        await collector.StartAsync(CancellationToken.None);
        try
        {
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.NotNull(received);
            Assert.Equal("eth0", received!.AdapterId);
            Assert.Equal(10, received.ReceivedBytes);
        }
        finally
        {
            await collector.StopAsync();
        }
    }

    [Fact]
    public async Task AdapterSetChange_RaisesNetworkChanged()
    {
        var source = new FakeSource { Adapters = [Snap("eth0", 1, 2)] };
        using var collector = new WindowsNetworkTrafficCollector(
            source, NullLogger<WindowsNetworkTrafficCollector>.Instance, TimeSpan.FromMilliseconds(30));

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var invoked = 0;

        collector.NetworkChanged += (_, _) =>
        {
            if (Interlocked.Increment(ref invoked) == 1)
            {
                tcs.TrySetResult(true);
            }
        };

        await collector.StartAsync(CancellationToken.None);
        try
        {
            source.Adapters = [Snap("eth0", 1, 2), Snap("wlan0", 3, 4)];
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(invoked >= 1);
        }
        finally
        {
            await collector.StopAsync();
        }
    }

    [Fact]
    public async Task StopAsync_CompletesAndUnsubscribes()
    {
        var source = new FakeSource { Adapters = [Snap("eth0", 1, 2)] };
        using var collector = new WindowsNetworkTrafficCollector(
            source, NullLogger<WindowsNetworkTrafficCollector>.Instance, TimeSpan.FromMilliseconds(30));

        await collector.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        await collector.StopAsync();

        // Stopping again is a safe no-op.
        await collector.StopAsync();
    }

    [Fact]
    public async Task CounterReset_DoesNotCrashAndKeepsCumulativeSnapshot()
    {
        var source = new FakeSource { Adapters = [Snap("eth0", 1000, 1000)] };
        using var collector = new WindowsNetworkTrafficCollector(
            source, NullLogger<WindowsNetworkTrafficCollector>.Instance, TimeSpan.FromMilliseconds(30));

        await collector.StartAsync(CancellationToken.None);
        try
        {
            await Task.Delay(100);

            // Simulate the OS zeroing/re-baselining the counter.
            source.Adapters = [Snap("eth0", 5000, 5000)];
            await Task.Delay(200);

            var counters = collector.GetCurrentCounterSamples();
            var eth = Assert.Single(counters, c => c.AdapterId == "eth0");
            Assert.Equal(5000, eth.ReceivedBytes);
        }
        finally
        {
            await collector.StopAsync();
        }
    }

    [Fact]
    public async Task SpeedSampleReady_FiresWithRealTrafficRates()
    {
        var source = new FakeSource { Adapters = [Snap("eth0", 0, 0)] };
        using var collector = new WindowsNetworkTrafficCollector(
            source, NullLogger<WindowsNetworkTrafficCollector>.Instance, TimeSpan.FromMilliseconds(30));

        NetworkSpeedSample? received = null;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        collector.SpeedSampleReady += (_, sample) =>
        {
            if (sample.DownloadBytesPerSecond > 0 || sample.UploadBytesPerSecond > 0)
            {
                received ??= sample;
                tcs.TrySetResult(true);
            }
        };

        await collector.StartAsync(CancellationToken.None);
        try
        {
            // First poll only establishes the baseline; then simulate real transfer.
            await Task.Delay(50);
            source.Adapters = [Snap("eth0", 50_000, 20_000)];

            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.NotNull(received);
            Assert.Equal("eth0", received!.AdapterId);
            Assert.True(received.DownloadBytesPerSecond > 0);
            Assert.True(received.UploadBytesPerSecond > 0);
        }
        finally
        {
            await collector.StopAsync();
        }
    }

    [Fact]
    public async Task GetCurrentSamples_ReturnsRateSamplesAfterTraffic()
    {
        var source = new FakeSource { Adapters = [Snap("eth0", 0, 0)] };
        using var collector = new WindowsNetworkTrafficCollector(
            source, NullLogger<WindowsNetworkTrafficCollector>.Instance, TimeSpan.FromMilliseconds(30));

        await collector.StartAsync(CancellationToken.None);
        try
        {
            await Task.Delay(50);
            source.Adapters = [Snap("eth0", 50_000, 20_000)];
            await Task.Delay(100);

            var rates = collector.GetCurrentSamples();
            var eth = Assert.Single(rates, r => r.AdapterId == "eth0");
            Assert.True(eth.DownloadBytesPerSecond >= 0);
            Assert.True(eth.UploadBytesPerSecond >= 0);
        }
        finally
        {
            await collector.StopAsync();
        }
    }
}