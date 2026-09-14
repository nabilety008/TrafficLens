using TrafficLens.Network.Process;

namespace TrafficLens.Network.Tests;

public sealed class ProcessTrafficAccountingEngineTests
{
    private const long TicksPerMs = TimeSpan.TicksPerMillisecond;
    private const long TicksPerSecond = 1000;

    private static long WallMs(long offsetMs) => DateTime.UtcNow.Ticks + offsetMs * TicksPerMs;

    private sealed class FakeMetadataProvider : IProcessMetadataProvider
    {
        public Func<ProcessInstanceId, ProcessMetadataResult> Resolver { get; set; } = _ => new(false, null);

        public int Calls { get; private set; }

        public ProcessMetadataResult Resolve(ProcessInstanceId identity)
        {
            Calls++;
            return Resolver(identity);
        }
    }

    private static ProcessMetadata Meta(string name, long startTicks = 0) =>
        new(name, $@"C:\fake\{name}.exe", IconAvailable: false, startTicks);

    private static ProcessTrafficAccountingEngine NewEngine(
        FakeMetadataProvider provider,
        int maxProcesses = 4096,
        double revalidationSeconds = 15.0) =>
        new(provider, maxProcesses, rateWindowSeconds: 3.0, idleRetentionSeconds: 120.0,
            unresolvedRetrySeconds: 10.0, revalidationSeconds);

    [Fact]
    public void Record_TracksSendsAndReceives()
    {
        var provider = new FakeMetadataProvider
        {
            Resolver = id => new(true, Meta("one", startTicks: 1_000))
        };
        var engine = NewEngine(provider);
        engine.Record(new NetworkTransferEvent(1, TransferDirection.Receive, 100, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(0)));
        engine.Record(new NetworkTransferEvent(1, TransferDirection.Send, 40, NetworkProtocolKind.Udp, IpVersionKind.IPv6, WallMs(5)));

        var samples = engine.Snapshot(0, TicksPerSecond, new DateTime(WallMs(10)));
        var sample = Assert.Single(samples);
        Assert.Equal(1, sample.ProcessId);
        Assert.Equal("one", sample.ProcessName);
        Assert.Equal(100, sample.DownloadBytes);
        Assert.Equal(40, sample.UploadBytes);
        Assert.Equal(140, sample.TotalBytes);

        var totals = engine.GetProtocolTotals(new ProcessInstanceId(1, 1_000));
        Assert.NotNull(totals);
        Assert.Equal(100, totals!.TcpReceivedBytes);
        Assert.Equal(40, totals.UdpSentBytes);
        Assert.Equal(100, totals.Ipv4ReceivedBytes);
        Assert.Equal(40, totals.Ipv6SentBytes);
    }

    [Fact]
    public void Snapshot_FirstCallSeedsWindow_RatesZero()
    {
        var provider = new FakeMetadataProvider { Resolver = id => new(true, Meta("app", startTicks: 1_000)) };
        var engine = NewEngine(provider);
        engine.Record(new NetworkTransferEvent(1, TransferDirection.Receive, 100, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(0)));

        var first = engine.Snapshot(0, TicksPerSecond, new DateTime(WallMs(10)));
        var second = engine.Snapshot(1000, TicksPerSecond, new DateTime(WallMs(1010)));
        Assert.Equal(0, first[0].DownloadBytesPerSecond);
        Assert.Equal(0, second[0].DownloadBytesPerSecond);
        Assert.Equal(0, second[0].UploadBytesPerSecond);
    }

    [Fact]
    public void Snapshot_ComputesRatesOverSlidingWindow()
    {
        var provider = new FakeMetadataProvider { Resolver = id => new(true, Meta("app", startTicks: 1_000)) };
        var engine = NewEngine(provider);
        engine.Record(new NetworkTransferEvent(1, TransferDirection.Receive, 100, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(0)));
        engine.Snapshot(0, TicksPerSecond, new DateTime(WallMs(10)));

        engine.Record(new NetworkTransferEvent(1, TransferDirection.Receive, 200, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(2_000)));
        var middle = engine.Snapshot(2_000, TicksPerSecond, new DateTime(WallMs(2_010)));
        Assert.Equal(100d, middle[0].DownloadBytesPerSecond, 2);

        engine.Record(new NetworkTransferEvent(1, TransferDirection.Receive, 300, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(3_000)));
        var last = engine.Snapshot(3_000, TicksPerSecond, new DateTime(WallMs(3_010)));
        Assert.Equal(500d / 3d, last[0].DownloadBytesPerSecond, 2);
    }

    [Fact]
    public void Record_IndependentProcesses_AreSeparateSamples()
    {
        var provider = new FakeMetadataProvider
        {
            Resolver = id =>
                id.ProcessId == 1
                    ? new(true, Meta("app", startTicks: 1_000))
                    : new(true, Meta("svc", startTicks: 2_000))
        };
        var engine = NewEngine(provider);
        engine.Record(new NetworkTransferEvent(1, TransferDirection.Receive, 50, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(0)));
        engine.Record(new NetworkTransferEvent(2, TransferDirection.Receive, 70, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(1)));

        var samples = engine.Snapshot(0, TicksPerSecond, new DateTime(WallMs(10)));
        Assert.Equal(2, samples.Count);
        Assert.Equal(2, samples[0].ProcessId);
        Assert.Equal("svc", samples[0].ProcessName);
        Assert.Equal(1, samples[1].ProcessId);
        Assert.Equal("app", samples[1].ProcessName);
    }

    [Fact]
    public void Record_SameExeDifferentPids_AreDistinctSamples()
    {
        var provider = new FakeMetadataProvider
        {
            Resolver = id => new(true, Meta("chrome", startTicks: id.ProcessId * 1_000))
        };
        var engine = NewEngine(provider);
        engine.Record(new NetworkTransferEvent(100, TransferDirection.Receive, 50, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(0)));
        engine.Record(new NetworkTransferEvent(200, TransferDirection.Receive, 90, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(1)));

        var samples = engine.Snapshot(0, TicksPerSecond, new DateTime(WallMs(10)));
        Assert.Equal(2, samples.Count);
        Assert.All(samples, s => Assert.Equal("chrome", s.ProcessName));
        Assert.Contains(samples, s => s.ProcessId == 100);
        Assert.Contains(samples, s => s.ProcessId == 200);
    }

    [Fact]
    public void UnknownPid_IsIsolatedAndNeverMergedIntoAnotherProcess()
    {
        var provider = new FakeMetadataProvider
        {
            Resolver = id =>
                id.ProcessId == 1
                    ? new(true, Meta("known", startTicks: 1_000))
                    : new(false, null)
        };
        var engine = NewEngine(provider);
        engine.Record(new NetworkTransferEvent(1, TransferDirection.Receive, 100, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(0)));
        engine.Record(new NetworkTransferEvent(999, TransferDirection.Receive, 40, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(1)));

        var samples = engine.Snapshot(0, TicksPerSecond, new DateTime(WallMs(10)));
        var unknown = Assert.Single(samples, s => s.ProcessId == 999);
        Assert.Equal("<unknown pid 999>", unknown.ProcessName);
        var known = Assert.Single(samples, s => s.ProcessId == 1);
        Assert.Equal(100, known.DownloadBytes);
    }

    [Fact]
    public void UnknownStartTime_LearnedStart_RebucketsAndSticks()
    {
        var startTicks = 42_000;
        var provider = new FakeMetadataProvider { Resolver = id => new(true, Meta("proc", startTicks)) };
        var engine = NewEngine(provider);
        engine.Record(new NetworkTransferEvent(7, TransferDirection.Receive, 100, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(0)));

        var first = engine.Snapshot(0, TicksPerSecond, new DateTime(WallMs(10)));
        var sample = Assert.Single(first);
        Assert.Equal(startTicks, sample.ProcessStartTimeUtcTicks);
        Assert.True(sample.HasKnownProcessStart);

        engine.Record(new NetworkTransferEvent(7, TransferDirection.Receive, 50, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(20)));
        var second = engine.Snapshot(100, TicksPerSecond, new DateTime(WallMs(120)));
        var after = Assert.Single(second);
        Assert.Equal(startTicks, after.ProcessStartTimeUtcTicks);
        Assert.Equal(150, after.DownloadBytes);
        Assert.Equal(1, engine.ProcessCount);
    }

    [Fact]
    public void PidReuse_NewInstance_IsIsolatedFromPrevious()
    {
        var startS1 = 10_000;
        var startS2 = 90_000;
        var provider = new FakeMetadataProvider();
        provider.Resolver = id => id.HasStartTime && id.StartTimeUtcTicks == startS1
            ? new(true, null)
            : (id.HasStartTime ? new(true, null) : new(true, Meta("proc", startS1)));
        var engine = NewEngine(provider, revalidationSeconds: 0);

        engine.Record(new NetworkTransferEvent(7, TransferDirection.Receive, 100, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(0)));
        var snap1 = engine.Snapshot(0, TicksPerSecond, new DateTime(WallMs(10)));
        Assert.Equal(startS1, snap1[0].ProcessStartTimeUtcTicks);

        provider.Resolver = id =>
        {
            if (id.HasStartTime && id.StartTimeUtcTicks == startS1)
            {
                return new(true, null);
            }

            return new(true, Meta("proc", startS2));
        };

        engine.Snapshot(1_100, TicksPerSecond, new DateTime(WallMs(1_200)));

        engine.Record(new NetworkTransferEvent(7, TransferDirection.Receive, 200, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(1_300)));
        var snap3 = engine.Snapshot(1_400, TicksPerSecond, new DateTime(WallMs(1_500)));

        var previous = Assert.Single(snap3, s => s.ProcessStartTimeUtcTicks == startS1);
        var current = Assert.Single(snap3, s => s.ProcessStartTimeUtcTicks == startS2);
        Assert.Equal(100, previous.DownloadBytes);
        Assert.Equal(200, current.DownloadBytes);
    }

    [Fact]
    public void ProcessExit_KeepsUnknownBucket_NoCrossMerge()
    {
        var provider = new FakeMetadataProvider { Resolver = id => new(false, null) };
        var engine = NewEngine(provider);
        engine.Record(new NetworkTransferEvent(5, TransferDirection.Receive, 50, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(0)));
        engine.Snapshot(0, TicksPerSecond, new DateTime(WallMs(10)));
        engine.Record(new NetworkTransferEvent(5, TransferDirection.Receive, 30, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(12)));
        var snap = engine.Snapshot(50, TicksPerSecond, new DateTime(WallMs(60)));

        var sample = Assert.Single(snap);
        Assert.Equal(80, sample.DownloadBytes);
        Assert.Equal("<unknown pid 5>", sample.ProcessName);
        Assert.Equal(2, engine.TotalEventsProcessed);
    }

    [Fact]
    public void Clear_ResetsAllState()
    {
        var provider = new FakeMetadataProvider { Resolver = id => new(true, Meta("app", startTicks: 1_000)) };
        var engine = NewEngine(provider);
        engine.Record(new NetworkTransferEvent(1, TransferDirection.Receive, 100, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(0)));
        engine.Snapshot(0, TicksPerSecond, new DateTime(WallMs(10)));

        engine.Clear();
        Assert.Equal(0, engine.ProcessCount);
        Assert.Empty(engine.GetCurrentSamples());

        engine.Record(new NetworkTransferEvent(1, TransferDirection.Receive, 20, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(20)));
        var snap = engine.Snapshot(50, TicksPerSecond, new DateTime(WallMs(60)));
        Assert.Equal(20, snap[0].DownloadBytes);
    }

    [Fact]
    public void OverCapacity_EvictsOldestByLastSeen()
    {
        var provider = new FakeMetadataProvider
        {
            Resolver = id => new(true, Meta($"p{id.ProcessId}", startTicks: id.ProcessId))
        };
        var engine = NewEngine(provider, maxProcesses: 3);
        for (var pid = 1; pid <= 5; pid++)
        {
            engine.Record(new NetworkTransferEvent(pid, TransferDirection.Receive, 10, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(pid * 10)));
        }

        engine.Snapshot(10_000, TicksPerSecond, new DateTime(WallMs(10_010)));
        Assert.True(engine.ProcessCount <= 3, $"expected <=3 counters, got {engine.ProcessCount}");
        var ids = engine.GetCurrentSamples().Select(s => s.ProcessId).ToHashSet();
        Assert.DoesNotContain(1, ids);
        Assert.DoesNotContain(2, ids);
        Assert.Contains(4, ids);
        Assert.Contains(5, ids);
    }

    [Fact]
    public void ProtocolTotals_ClassifyTcpUdpAndIpVersions()
    {
        var startTicks = 7_777;
        var provider = new FakeMetadataProvider { Resolver = id => new(true, Meta("app", startTicks)) };
        var engine = NewEngine(provider);
        engine.Record(new NetworkTransferEvent(1, TransferDirection.Receive, 100, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(0)));
        engine.Record(new NetworkTransferEvent(1, TransferDirection.Send, 60, NetworkProtocolKind.Tcp, IpVersionKind.IPv6, WallMs(1)));
        engine.Record(new NetworkTransferEvent(1, TransferDirection.Receive, 40, NetworkProtocolKind.Udp, IpVersionKind.IPv4, WallMs(2)));
        engine.Record(new NetworkTransferEvent(1, TransferDirection.Send, 25, NetworkProtocolKind.Udp, IpVersionKind.IPv6, WallMs(3)));
        engine.Record(new NetworkTransferEvent(1, TransferDirection.Receive, 35, NetworkProtocolKind.Tcp, IpVersionKind.IPv6, WallMs(4)));
        engine.Record(new NetworkTransferEvent(1, TransferDirection.Send, 15, NetworkProtocolKind.Udp, IpVersionKind.IPv4, WallMs(5)));

        var sample = engine.Snapshot(0, TicksPerSecond, new DateTime(WallMs(10)))[0];
        var totals = engine.GetProtocolTotals(new ProcessInstanceId(1, startTicks));

        Assert.Equal(175, sample.DownloadBytes);
        Assert.Equal(135, totals!.TcpReceivedBytes);
        Assert.Equal(60, totals.TcpSentBytes);
        Assert.Equal(40, totals.UdpReceivedBytes);
        Assert.Equal(40, totals.UdpSentBytes);
        Assert.Equal(140, totals.Ipv4ReceivedBytes);
        Assert.Equal(15, totals.Ipv4SentBytes);
        Assert.Equal(35, totals.Ipv6ReceivedBytes);
        Assert.Equal(85, totals.Ipv6SentBytes);
        Assert.Equal(totals.TcpReceivedBytes + totals.UdpReceivedBytes, totals.TotalReceivedBytes);
        Assert.Equal(totals.Ipv4ReceivedBytes + totals.Ipv6ReceivedBytes, totals.TotalReceivedBytes);
    }

    [Fact]
    public void NoTraffic_ProducesEmptySnapshot()
    {
        var engine = NewEngine(new FakeMetadataProvider());
        var samples = engine.Snapshot(0, TicksPerSecond, DateTime.UtcNow);
        Assert.Empty(samples);
    }

    [Fact]
    public void Snapshot_RatesUseMeasuredElapsed_NotAssumedSecond()
    {
        var provider = new FakeMetadataProvider { Resolver = id => new(true, Meta("app", startTicks: 1_000)) };
        var engine = NewEngine(provider);
        engine.Record(new NetworkTransferEvent(1, TransferDirection.Receive, 100, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(0)));
        engine.Snapshot(0, TicksPerSecond, new DateTime(WallMs(10)));

        engine.Record(new NetworkTransferEvent(1, TransferDirection.Receive, 300, NetworkProtocolKind.Tcp, IpVersionKind.IPv4, WallMs(500)));
        var snap = engine.Snapshot(500, TicksPerSecond, new DateTime(WallMs(510)));
        Assert.Equal(600d, snap[0].DownloadBytesPerSecond, 2);
    }
}