using TrafficLens.Network.Adapters;

namespace TrafficLens.Network.Tests;

public class AdapterSnapshotCacheTests
{
    private sealed class TestTimeProvider : TimeProvider
    {
        public DateTimeOffset NowUtc { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => NowUtc;
    }

    private static RawAdapterSnapshot Dummy(int id) =>
        new(
            Id: $"adapter-{id}",
            Name: $"Adapter {id}",
            Description: string.Empty,
            PhysicalAddress: string.Empty,
            InterfaceType: System.Net.NetworkInformation.NetworkInterfaceType.Ethernet,
            IsUp: true,
            HasGateway: true,
            IpAddress: "192.168.1.10",
            LinkSpeedBitsPerSecond: 1000000000,
            ReceivedBytes: 100,
            SentBytes: 100);

    [Fact]
    public void WithinRefreshWindow_ServesCachedSnapshot()
    {
        var clock = new TestTimeProvider();
        var loadCount = 0;
        var cache = new AdapterSnapshotCache(
            () =>
            {
                loadCount++;
                return new List<RawAdapterSnapshot> { Dummy(1) };
            },
            refreshInterval: TimeSpan.FromMinutes(1),
            timeProvider: clock);

        var first = cache.GetSnapshot();
        var second = cache.GetSnapshot();

        Assert.Same(first, second);
        Assert.Equal(1, loadCount);
    }

    [Fact]
    public void AfterWindowElapses_ReloadsFromSource()
    {
        var clock = new TestTimeProvider();
        var loadCount = 0;
        var cache = new AdapterSnapshotCache(
            () =>
            {
                loadCount++;
                return new List<RawAdapterSnapshot> { Dummy(loadCount) };
            },
            refreshInterval: TimeSpan.FromSeconds(1),
            timeProvider: clock);

        _ = cache.GetSnapshot();
        clock.NowUtc = clock.NowUtc.AddSeconds(2);

        var fresh = cache.GetSnapshot();

        Assert.Equal(2, loadCount);
        Assert.Equal("adapter-2", fresh[0].Id);
    }

    [Fact]
    public void Invalidate_ForcesReloadOnNextCall()
    {
        var clock = new TestTimeProvider();
        var loadCount = 0;
        var cache = new AdapterSnapshotCache(
            () =>
            {
                loadCount++;
                return new List<RawAdapterSnapshot> { Dummy(loadCount) };
            },
            refreshInterval: TimeSpan.FromMinutes(1),
            timeProvider: clock);

        _ = cache.GetSnapshot();
        cache.Invalidate();

        var fresh = cache.GetSnapshot();

        Assert.Equal(2, loadCount);
        Assert.Equal("adapter-2", fresh[0].Id);
    }

    [Fact]
    public void ConcurrentCalls_WithinWindow_LoadOnlyOnce()
    {
        var clock = new TestTimeProvider();
        var loadCount = 0;
        var cache = new AdapterSnapshotCache(
            () =>
            {
                Interlocked.Increment(ref loadCount);
                return new List<RawAdapterSnapshot> { Dummy(loadCount) };
            },
            refreshInterval: TimeSpan.FromHours(1),
            timeProvider: clock);

        Parallel.For(0, 64, _ =>
        {
            for (var i = 0; i < 20; i++)
            {
                cache.GetSnapshot();
            }
        });

        Assert.Equal(1, loadCount);
    }

    [Fact]
    public void FailedEnumeration_IsNotCached_AndRetriesNextCall()
    {
        var clock = new TestTimeProvider();
        var loadCount = 0;
        var cache = new AdapterSnapshotCache(
            () =>
            {
                loadCount++;
                if (loadCount == 1)
                {
                    throw new InvalidOperationException("boom");
                }

                return new List<RawAdapterSnapshot> { Dummy(2) };
            },
            refreshInterval: TimeSpan.FromMinutes(1),
            timeProvider: clock);

        Assert.Throws<InvalidOperationException>(() => cache.GetSnapshot());

        var fresh = cache.GetSnapshot();

        Assert.Equal(2, loadCount);
        Assert.Single(fresh);
    }
}