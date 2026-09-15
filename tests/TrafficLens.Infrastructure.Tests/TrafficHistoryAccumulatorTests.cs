using TrafficLens.Core.History;
using TrafficLens.Core.Models;

namespace TrafficLens.Infrastructure.Tests;

public class TrafficHistoryAccumulatorTests
{
    private static DateTime Utc(int year = 2026, int month = 6, int day = 15, int hour = 12, int minute = 0, int second = 0) =>
        new(year, month, day, hour, minute, second, DateTimeKind.Utc);

    private static NetworkCounterSample Sample(
        string id = "eth0",
        long received = 0,
        long sent = 0,
        DateTime? timestamp = null) =>
        new(id, id, received, sent, timestamp ?? Utc());

    [Fact]
    public void FirstObservationEstablishesBaseline_NoDelta()
    {
        var acc = new TrafficHistoryAccumulator();
        acc.Ingest(Sample(received: 1000, sent: 500), NetworkAdapterKind.Ethernet);
        Assert.Equal(0, acc.PendingBucketCount);
    }

    [Fact]
    public void SecondObservationEmitsDelta()
    {
        var acc = new TrafficHistoryAccumulator();
        acc.Ingest(Sample(received: 1000, sent: 500), NetworkAdapterKind.Ethernet);
        acc.Ingest(Sample(received: 1100, sent: 520), NetworkAdapterKind.Ethernet);
        Assert.Equal(1, acc.PendingBucketCount);
        var buckets = acc.DrainAll(Utc(minute: 1));
        Assert.Single(buckets);
        Assert.Equal(100, buckets[0].DownloadBytes);
        Assert.Equal(20, buckets[0].UploadBytes);
        Assert.Equal(60, buckets[0].DurationSeconds);
    }

    [Fact]
    public void CounterReset_Rebaselines_NoNegativeDelta()
    {
        var acc = new TrafficHistoryAccumulator();
        acc.Ingest(Sample(received: 5000, sent: 2000), NetworkAdapterKind.Ethernet);
        acc.Ingest(Sample(received: 1000, sent: 500), NetworkAdapterKind.Ethernet);
        Assert.Equal(0, acc.PendingBucketCount);
    }

    [Fact]
    public void TunnelExcluded_ByDefault()
    {
        var acc = new TrafficHistoryAccumulator();
        acc.Ingest(Sample(received: 1000, sent: 500), NetworkAdapterKind.Ethernet);
        acc.Ingest(Sample(received: 1100, sent: 520, id: "tun0"), NetworkAdapterKind.Tunnel);
        acc.Ingest(Sample(received: 1200, sent: 600), NetworkAdapterKind.Ethernet);
        var buckets = acc.DrainAll(Utc(minute: 1));
        Assert.Single(buckets);
        Assert.Equal(200, buckets[0].DownloadBytes);
        Assert.Equal(100, buckets[0].UploadBytes);
    }

    [Fact]
    public void MultipleAdapters_SumWithinSameMinute()
    {
        var acc = new TrafficHistoryAccumulator();
        acc.Ingest(Sample(id: "a", received: 1000, sent: 0), NetworkAdapterKind.Ethernet);
        acc.Ingest(Sample(id: "b", received: 2000, sent: 0), NetworkAdapterKind.Wireless);
        acc.Ingest(Sample(id: "a", received: 1200, sent: 0), NetworkAdapterKind.Ethernet);
        acc.Ingest(Sample(id: "b", received: 2300, sent: 0), NetworkAdapterKind.Wireless);
        var buckets = acc.DrainAll(Utc(minute: 1));
        Assert.Single(buckets);
        Assert.Equal(500, buckets[0].DownloadBytes);
        Assert.Equal(0, buckets[0].UploadBytes);
    }

    [Fact]
    public void DrainCompleted_ExcludesOpenMinute()
    {
        var acc = new TrafficHistoryAccumulator();
        acc.Ingest(Sample(received: 1000, sent: 500, timestamp: Utc(minute: 0)), NetworkAdapterKind.Ethernet);
        acc.Ingest(Sample(received: 1100, sent: 520, timestamp: Utc(minute: 0, second: 30)), NetworkAdapterKind.Ethernet);
        var completed = acc.DrainCompleted(Utc(minute: 0, second: 59));
        Assert.Empty(completed);
        Assert.Equal(1, acc.PendingBucketCount);
    }

    [Fact]
    public void DrainCompleted_IncludesFullMinutes()
    {
        var acc = new TrafficHistoryAccumulator();
        acc.Ingest(Sample(received: 1000, sent: 500, timestamp: Utc(minute: 0)), NetworkAdapterKind.Ethernet);
        acc.Ingest(Sample(received: 1100, sent: 520, timestamp: Utc(minute: 0, second: 30)), NetworkAdapterKind.Ethernet);
        acc.Ingest(Sample(received: 1200, sent: 550, timestamp: Utc(minute: 1, second: 10)), NetworkAdapterKind.Ethernet);
        var completed = acc.DrainCompleted(Utc(minute: 1, second: 40));
        Assert.Single(completed);
        Assert.Equal(60, completed[0].DurationSeconds);
        Assert.Equal(1, acc.PendingBucketCount);
    }

    [Fact]
    public void DrainAll_IncludesOpenMinute_ClampedToOneToSixty()
    {
        var acc = new TrafficHistoryAccumulator();
        acc.Ingest(Sample(received: 1000, sent: 500, timestamp: Utc(minute: 5, second: 10)), NetworkAdapterKind.Ethernet);
        acc.Ingest(Sample(received: 1100, sent: 520, timestamp: Utc(minute: 5, second: 20)), NetworkAdapterKind.Ethernet);
        var buckets = acc.DrainAll(Utc(minute: 5, second: 45));
        Assert.Single(buckets);
        Assert.Equal(45, buckets[0].DurationSeconds);
        Assert.Equal(0, acc.PendingBucketCount);
    }

    [Fact]
    public void DrainAll_EmptyReturnsEmpty()
    {
        var acc = new TrafficHistoryAccumulator();
        Assert.Empty(acc.DrainAll(Utc()));
    }

    [Fact]
    public void BaselineCount_TracksAdapters()
    {
        var acc = new TrafficHistoryAccumulator();
        acc.Ingest(Sample(id: "a"), NetworkAdapterKind.Ethernet);
        acc.Ingest(Sample(id: "b"), NetworkAdapterKind.Ethernet);
        Assert.Equal(2, acc.BaselineCount);
    }
}
