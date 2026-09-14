using TrafficLens.Core.Models;
using TrafficLens.Network.Calculation;

namespace TrafficLens.Network.Tests;

public sealed class SpeedRateTrackerTests
{
    private const double TicksPerSecond = 10_000_000;

    private static long T(double seconds) => (long)(seconds * TicksPerSecond);

    private static NetworkCounterSample C(string id, long received, long sent) =>
        new(id, id, received, sent, DateTime.UnixEpoch);

    private static Dictionary<string, NetworkCounterSample> Counters(params NetworkCounterSample[] items) =>
        items.ToDictionary(s => s.AdapterId, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void FirstSample_OnlyEstablishesBaseline()
    {
        var tracker = new SpeedRateTracker();

        var first = tracker.Track(Counters(C("eth0", 10_000_000, 5_000_000)), T(0), TicksPerSecond);

        Assert.Empty(first);
        Assert.Equal(1, tracker.BaselineCount);
    }

    [Fact]
    public void SecondSample_ComputesRateWithoutSpike()
    {
        var tracker = new SpeedRateTracker();
        tracker.Track(Counters(C("eth0", 0, 0)), T(0), TicksPerSecond);

        var rates = tracker.Track(Counters(C("eth0", 1_250_000, 2_500_000)), T(1), TicksPerSecond);

        var rate = Assert.Single(rates);
        Assert.Equal("eth0", rate.AdapterId);
        Assert.Equal(1_250_000, rate.DownloadBytesPerSecond);
        Assert.Equal(2_500_000, rate.UploadBytesPerSecond);
    }

    [Fact]
    public void NonExactElapsed_ComputesAverageRateOverActualInterval()
    {
        var tracker = new SpeedRateTracker();
        tracker.Track(Counters(C("eth0", 1000, 1000)), T(0), TicksPerSecond);

        var rates = tracker.Track(Counters(C("eth0", 1500, 1250)), T(1.25), TicksPerSecond);

        var rate = Assert.Single(rates);
        Assert.Equal(400, rate.DownloadBytesPerSecond);
        Assert.Equal(200, rate.UploadBytesPerSecond);
    }

    [Fact]
    public void CounterDecrease_RebaselinesWithoutEmittingSpike()
    {
        var tracker = new SpeedRateTracker();
        tracker.Track(Counters(C("eth0", 1000, 1000)), T(0), TicksPerSecond);

        Assert.Empty(tracker.Track(Counters(C("eth0", 500, 400)), T(1), TicksPerSecond));

        var rates = tracker.Track(Counters(C("eth0", 700, 600)), T(2), TicksPerSecond);
        var rate = Assert.Single(rates);
        Assert.Equal(200, rate.DownloadBytesPerSecond);
        Assert.Equal(200, rate.UploadBytesPerSecond);
    }

    [Fact]
    public void ZeroElapsed_DoesNotEmitAndRefreshesBaseline()
    {
        var tracker = new SpeedRateTracker();
        tracker.Track(Counters(C("eth0", 100, 50)), T(1), TicksPerSecond);

        Assert.Empty(tracker.Track(Counters(C("eth0", 200, 100)), T(1), TicksPerSecond));

        var rates = tracker.Track(Counters(C("eth0", 300, 150)), T(2), TicksPerSecond);
        var rate = Assert.Single(rates);
        Assert.Equal(100, rate.DownloadBytesPerSecond);
    }

    [Fact]
    public void AdapterDisappearance_PrunesBaselineAndRebaselinesOnReturn()
    {
        var tracker = new SpeedRateTracker();
        tracker.Track(Counters(C("eth0", 100, 50)), T(0), TicksPerSecond);

        tracker.Track(Counters(C("wlan0", 10, 10)), T(1), TicksPerSecond);
        Assert.Equal(1, tracker.BaselineCount);

        Assert.Empty(tracker.Track(Counters(C("eth0", 120, 60)), T(2), TicksPerSecond));

        var rates = tracker.Track(Counters(C("eth0", 220, 110)), T(3), TicksPerSecond);
        var rate = Assert.Single(rates);
        Assert.Equal(100, rate.DownloadBytesPerSecond);
    }

    [Fact]
    public void AdapterReplacement_NewIdRebaselines()
    {
        var tracker = new SpeedRateTracker();
        tracker.Track(Counters(C("eth0", 100, 50)), T(0), TicksPerSecond);

        Assert.Empty(tracker.Track(Counters(C("eth0new", 100, 50)), T(1), TicksPerSecond));

        var rates = tracker.Track(Counters(C("eth0new", 200, 100)), T(2), TicksPerSecond);
        var rate = Assert.Single(rates);
        Assert.Equal(100, rate.DownloadBytesPerSecond);
    }

    [Fact]
    public void MultipleAdapters_KeepIndependentBaselines()
    {
        var tracker = new SpeedRateTracker();
        tracker.Track(Counters(C("eth0", 0, 0), C("wg0", 0, 0)), T(0), TicksPerSecond);

        var rates = tracker.Track(
            Counters(C("eth0", 500, 300), C("wg0", 700, 900)),
            T(1),
            TicksPerSecond);

        Assert.Equal(2, rates.Count);
        Assert.Equal(500, Assert.Single(rates, r => r.AdapterId == "eth0").DownloadBytesPerSecond);
        Assert.Equal(900, Assert.Single(rates, r => r.AdapterId == "wg0").UploadBytesPerSecond);
    }
}