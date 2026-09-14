using TrafficLens.Core.Graph;

namespace TrafficLens.Network.Tests;

public sealed class TrafficSampleBufferTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Add_DuplicateOrOlderTimestamps_AreRejected()
    {
        var buffer = Buffer();
        Assert.True(buffer.Add(T0, 100, 5));
        Assert.False(buffer.Add(T0, 200, 6));
        Assert.False(buffer.Add(T0.AddSeconds(-1), 50, 2));
        Assert.Equal(1, buffer.Count);
        Assert.Equal(T0, buffer.LatestTimestamp);
    }

    [Fact]
    public void Capacity_IsBounded_AndOldestDropped()
    {
        var buffer = new TrafficSampleBuffer(TimeSpan.FromHours(1), capacity: 4);
        for (var i = 0; i < 10; i++)
        {
            buffer.Add(T0.AddSeconds(i), 1000 + i, 100);
        }

        Assert.Equal(4, buffer.Count);
        Assert.Equal(T0.AddSeconds(9), buffer.LatestTimestamp);
    }

    [Fact]
    public void TimeBasedExpiration_DiscardsBeyondRetention()
    {
        var buffer = new TrafficSampleBuffer(TimeSpan.FromSeconds(10), capacity: 100);
        var newest = T0.AddSeconds(98);
        for (var i = 0; i < 50; i++)
        {
            buffer.Add(T0.AddSeconds(i * 2), 1, 1);
        }

        Assert.Equal(6, buffer.Count);
        var slice = buffer.Slice(TimeSpan.FromSeconds(8), newest);
        Assert.Equal(5, slice.Count);

        var longSlice = buffer.Slice(TimeSpan.FromMinutes(5), newest);
        Assert.Equal(6, longSlice.Count);
        Assert.All(slice, p => Assert.True(p.Timestamp >= newest.AddSeconds(-8)));
    }

    [Fact]
    public void Slice_ReturnsChronologicalOrder()
    {
        var buffer = Buffer();
        var timestamps = new[] { T0, T0.AddSeconds(2), T0.AddSeconds(5), T0.AddSeconds(9) };
        foreach (var t in timestamps)
        {
            buffer.Add(t, 100, 10);
        }

        var slice = buffer.Slice(TimeSpan.FromSeconds(60), T0.AddSeconds(9));
        Assert.Equal(timestamps, slice.Select(p => p.Timestamp).ToArray());
    }

    [Fact]
    public void NonExactIntervals_AreHonoredByTimestamps()
    {
        var buffer = Buffer();
        buffer.Add(T0, 10, 1);
        buffer.Add(T0.AddMilliseconds(350), 10, 1);
        buffer.Add(T0.AddSeconds(1.2), 10, 1);
        buffer.Add(T0.AddSeconds(3), 10, 1);

        var slice = buffer.Slice(TimeSpan.FromSeconds(2), T0.AddSeconds(3));
        Assert.Equal(new[] { T0.AddSeconds(1.2), T0.AddSeconds(3) }, slice.Select(p => p.Timestamp).ToArray());
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(300)]
    public void Slicing_ReturnsOnlyPointsInsideWindow(int seconds)
    {
        var buffer = Buffer();
        var now = T0.AddMinutes(10);
        for (var i = 0; i < 400; i++)
        {
            buffer.Add(now.AddSeconds(i - 399), i, i);
        }

        var slice = buffer.Slice(TimeSpan.FromSeconds(seconds), now);
        Assert.Equal(seconds + 1, slice.Count);
        Assert.All(slice, p => Assert.True(p.Timestamp >= now.AddSeconds(-seconds)));
    }

    [Fact]
    public void RangeSwitching_DoesNotClearHistory()
    {
        var buffer = Buffer();
        for (var i = 0; i < 300; i++)
        {
            buffer.Add(T0.AddSeconds(i), i, i);
        }

        var all = buffer.Slice(TimeSpan.FromSeconds(300), T0.AddSeconds(300));
        var shortSlice = buffer.Slice(TimeSpan.FromSeconds(30), T0.AddSeconds(300));
        var longAgain = buffer.Slice(TimeSpan.FromSeconds(300), T0.AddSeconds(300));

        Assert.Equal(300, all.Count);
        Assert.Equal(30, shortSlice.Count);
        Assert.Equal(all, longAgain);
        Assert.Equal(300, buffer.Count);
    }

    [Fact]
    public void ZeroTraffic_SamplesAreRetained()
    {
        var buffer = Buffer();
        for (var i = 0; i < 60; i++)
        {
            buffer.Add(T0.AddSeconds(i), 0, 0);
        }

        var slice = buffer.Slice(TimeSpan.FromSeconds(60), T0.AddSeconds(60));
        Assert.Equal(60, slice.Count);
        Assert.All(slice, p => Assert.Equal(0, p.DownloadBytesPerSecond + p.UploadBytesPerSecond));
    }

    [Fact]
    public void LargeValues_AreRetainedWithoutOverflow()
    {
        var buffer = Buffer();
        buffer.Add(T0, 1_000_000_000, 900_000_000);
        var single = buffer.Slice(TimeSpan.FromSeconds(60), T0);
        Assert.Equal(1_000_000_000, single[0].DownloadBytesPerSecond);
        Assert.Equal(900_000_000, single[0].UploadBytesPerSecond);
    }

    [Fact]
    public void GapReconnect_PreservesHistoryBeforeAndAfterGap()
    {
        var buffer = Buffer();
        buffer.Add(T0, 1, 1);
        buffer.Add(T0.AddSeconds(1), 2, 2);
        buffer.Add(T0.AddSeconds(11), 3, 3);

        var gapSlice = buffer.Slice(TimeSpan.FromSeconds(2), T0.AddSeconds(11));
        Assert.Equal(new[] { T0.AddSeconds(11) }, gapSlice.Select(p => p.Timestamp).ToArray());

        buffer.Add(T0.AddSeconds(12), 4, 4);
        Assert.Equal(T0.AddSeconds(12), buffer.LatestTimestamp);
        Assert.Equal(4, buffer.Count);
        var reconnectSlice = buffer.Slice(TimeSpan.FromSeconds(30), T0.AddSeconds(12));
        Assert.Equal(4, reconnectSlice.Count);

        var preGapSlice = buffer.Slice(TimeSpan.FromSeconds(60), T0.AddSeconds(2));
        Assert.Contains(T0, preGapSlice.Select(p => p.Timestamp));
        Assert.Contains(T0.AddSeconds(1), preGapSlice.Select(p => p.Timestamp));
    }

    [Fact]
    public void EmptyBuffer_ReturnsEmptySlice()
    {
        var buffer = Buffer();
        Assert.Empty(buffer.Slice(TimeSpan.FromSeconds(60), T0));
        Assert.Null(buffer.LatestTimestamp);
    }

    private static TrafficSampleBuffer Buffer() =>
        new(maxRetention: TimeSpan.FromMinutes(6), capacity: 1000);
}

public sealed class AdaptiveGraphScaleTests
{
    [Fact]
    public void ZeroTraffic_ReturnsFloor_NoDivideByZero()
    {
        var scale = new AdaptiveGraphScale();
        for (var i = 0; i < 10; i++)
        {
            var max = scale.Update(new[]
            {
                new TrafficGraphPoint(MaxTimestamp.AddSeconds(i), 0, 0)
            });

            Assert.True(max >= 1, "scale must never be zero");
            Assert.Equal(max, scale.Current);
        }

        Assert.True(scale.Current >= 1);
    }

    private static DateTime MaxTimestamp => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EmptySamples_ReturnFloor()
    {
        var scale = new AdaptiveGraphScale();
        Assert.Equal(AdaptiveGraphScale.FloorBytesPerSecond, scale.Update(Array.Empty<TrafficGraphPoint>()));
    }

    [Fact]
    public void Peak_IsCoveredByNiceCeiling()
    {
        var scale = new AdaptiveGraphScale();
        for (var ticks = 1L; ticks <= 1_000_000_000; ticks *= 10)
        {
            var max = scale.Update(new[] { new TrafficGraphPoint(new DateTime(2026, 1, 1), ticks, ticks / 2) });
            Assert.True(max >= ticks, $"scale {max} < peak {ticks}");
        }
    }

    [Fact]
    public void BothSeries_ShareOneScale()
    {
        var scale = new AdaptiveGraphScale();
        var max = scale.Update(new[]
        {
            new TrafficGraphPoint(new DateTime(2026, 1, 1), 1_000, 900_000)
        });

        Assert.True(max >= 900_000);
        Assert.Equal(scale.Current, max);
    }

    [Fact]
    public void Spike_GrowsImmediately()
    {
        var scale = new AdaptiveGraphScale();
        scale.Update(new[] { new TrafficGraphPoint(new DateTime(2026, 1, 1), 100, 0) });
        var before = scale.Current;

        var max = scale.Update(new[] { new TrafficGraphPoint(new DateTime(2026, 1, 1, 0, 0, 1), 5_000_000, 0) });
        Assert.True(max > before);
        Assert.True(max >= 5_000_000);
    }

    [Fact]
    public void Shrink_IsHysteretic_NotFlickering()
    {
        var scale = new AdaptiveGraphScale();
        scale.Update(new[] { new TrafficGraphPoint(new DateTime(2026, 1, 1), 8_000_000, 0) });
        var peakScale = scale.Current;

        var singleDip = scale.Update(new[] { new TrafficGraphPoint(new DateTime(2026, 1, 1, 0, 0, 1), 1_000, 0) });
        Assert.Equal(peakScale, singleDip);

        for (var i = 0; i < 20; i++)
        {
            scale.Update(new[] { new TrafficGraphPoint(new DateTime(2026, 1, 1).AddSeconds(i + 2), 1_000, 0) });
        }

        Assert.True(scale.Current < peakScale);
        Assert.True(scale.Current >= AdaptiveGraphScale.FloorBytesPerSecond);
    }

    [Fact]
    public void NiceCeil_RoundsToReadableNumbers()
    {
        Assert.Equal(10, AdaptiveGraphScale.NiceCeil(6));
        Assert.Equal(20, AdaptiveGraphScale.NiceCeil(11));
        Assert.Equal(50, AdaptiveGraphScale.NiceCeil(41));
        Assert.Equal(200, AdaptiveGraphScale.NiceCeil(105));
        Assert.Equal(500, AdaptiveGraphScale.NiceCeil(300));
        Assert.Equal(2_000, AdaptiveGraphScale.NiceCeil(1_234));
        Assert.Equal(1_000_000, AdaptiveGraphScale.NiceCeil(999_999));
    }
}