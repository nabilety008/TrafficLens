using TrafficLens.Core.Graph;

namespace TrafficLens.Network.Tests;

public sealed class GraphScaleLabelsTests
{
    [Fact]
    public void Levels_AlwaysThree_BottomMiddleTop()
    {
        var levels = GraphScaleLabels.Levels(4_000_000);

        Assert.Equal(3, levels.Count);
        Assert.Equal(Position.Bottom, levels[0].Position);
        Assert.Equal(Position.Middle, levels[1].Position);
        Assert.Equal(Position.Top, levels[2].Position);
    }

    [Fact]
    public void Levels_ZeroOrLowTraffic_NoNegativeNoZeroMax()
    {
        foreach (var max in new[] { 0L, 1L, 100L, AdaptiveGraphScale.FloorBytesPerSecond })
        {
            var levels = GraphScaleLabels.Levels(max);

            Assert.All(levels, l => Assert.True(l.BytesPerSecond >= 0));
            Assert.Equal(0, levels[0].BytesPerSecond);
            Assert.True(levels[2].BytesPerSecond >= 1);
        }
    }

    [Fact]
    public void Levels_TopLevel_EqualsScaleMax()
    {
        long scaleMax = AdaptiveGraphScale.NiceCeil(3_500_000);
        var levels = GraphScaleLabels.Levels(scaleMax);

        Assert.Equal(scaleMax, levels[2].BytesPerSecond);
        Assert.Equal(scaleMax / 2, levels[1].BytesPerSecond);
    }

    [Theory]
    [InlineData(500L)]
    [InlineData(50_000L)]
    [InlineData(5_000_000L)]
    [InlineData(500_000_000L)]
    public void Levels_FiniteAndNonNegative_ForAllRanges(long scaleMax)
    {
        var levels = GraphScaleLabels.Levels(scaleMax);

        Assert.All(levels, l =>
        {
            Assert.True(l.BytesPerSecond >= 0);
            Assert.True(l.BytesPerSecond <= long.MaxValue / 2);
        });
    }

    [Fact]
    public void Levels_MatchAdaptiveScaleOutput()
    {
        var scale = new AdaptiveGraphScale();
        var max = scale.Update(new[]
        {
            new TrafficGraphPoint(new DateTime(2026, 1, 1), 4_000_000, 2_000_000)
        });

        var levels = GraphScaleLabels.Levels(max);
        Assert.Equal(max, levels[2].BytesPerSecond);
        Assert.Equal(max / 2, levels[1].BytesPerSecond);
    }
}

public sealed class GraphHoverResolverTests
{
    private static readonly DateTime Reference = new(2026, 1, 1, 0, 1, 0, DateTimeKind.Utc);
    private const double WindowSeconds = 60;

    private static TrafficGraphPoint[] Samples() => new[]
    {
        new TrafficGraphPoint(Reference.AddSeconds(-60), 100, 10),
        new TrafficGraphPoint(Reference.AddSeconds(-30), 200, 20),
        new TrafficGraphPoint(Reference, 300, 30)
    };

    [Fact]
    public void EmptyBuffer_ReturnsNull()
    {
        var sample = GraphHoverResolver.Resolve(Array.Empty<TrafficGraphPoint>(), 50, 600, WindowSeconds, Reference);
        Assert.Null(sample);
    }

    [Fact]
    public void OneSample_AlwaysResolvesToIt()
    {
        var points = new[] { new TrafficGraphPoint(Reference, 500, 50) };
        var nearLeft = GraphHoverResolver.Resolve(points, 0, 600, WindowSeconds, Reference);
        var nearRight = GraphHoverResolver.Resolve(points, 600, 600, WindowSeconds, Reference);

        Assert.NotNull(nearLeft);
        Assert.Equal(500, nearLeft!.Value.DownloadBytesPerSecond);
        Assert.Equal(nearLeft, nearRight);
    }

    [Fact]
    public void PointerNearFirstSample_ResolvesFirst()
    {
        var sample = GraphHoverResolver.Resolve(Samples(), 5, 600, WindowSeconds, Reference);
        Assert.Equal(100, sample!.Value.DownloadBytesPerSecond);
    }

    [Fact]
    public void PointerNearMiddleSample_ResolvesMiddle()
    {
        var sample = GraphHoverResolver.Resolve(Samples(), 300, 600, WindowSeconds, Reference);
        Assert.Equal(200, sample!.Value.DownloadBytesPerSecond);
    }

    [Fact]
    public void PointerNearLastSample_ResolvesLast()
    {
        var sample = GraphHoverResolver.Resolve(Samples(), 598, 600, WindowSeconds, Reference);
        Assert.Equal(300, sample!.Value.DownloadBytesPerSecond);
    }

    [Theory]
    [InlineData(-1000.0)]
    [InlineData(1000.0)]
    public void PointerBeyondEdges_ClampsToNearestEnd(double pointerX)
    {
        var expected = pointerX < 0 ? 100L : 300L;
        var sample = GraphHoverResolver.Resolve(Samples(), pointerX, 600, WindowSeconds, Reference);
        Assert.Equal(expected, sample!.Value.DownloadBytesPerSecond);
    }

    [Fact]
    public void ZeroPlotWidth_ReturnsNull()
    {
        var sample = GraphHoverResolver.Resolve(Samples(), 0, 0, WindowSeconds, Reference);
        Assert.Null(sample);
    }

    [Fact]
    public void ResolvedTimestamps_MatchBufferSamples()
    {
        var points = Samples();
        var sample = GraphHoverResolver.Resolve(points, 0, 600, WindowSeconds, Reference);

        Assert.Equal(points[0].Timestamp, sample!.Value.Timestamp);
    }
}

public sealed class HistoryBarHoverResolverTests
{
    [Fact]
    public void EmptySeries_ReturnsNull()
    {
        Assert.Null(HistoryBarHoverResolver.ResolveIndex(0, 100, 600));
    }

    [Fact]
    public void ZeroPlotWidth_ReturnsNull()
    {
        Assert.Null(HistoryBarHoverResolver.ResolveIndex(5, 100, 0));
    }

    [Fact]
    public void SingleBar_AnyPointer_ReturnsIndex0()
    {
        Assert.Equal(0, HistoryBarHoverResolver.ResolveIndex(1, 0, 600));
        Assert.Equal(0, HistoryBarHoverResolver.ResolveIndex(1, 599, 600));
    }

    [Fact]
    public void FirstBar_NearLeftEdge()
    {
        Assert.Equal(0, HistoryBarHoverResolver.ResolveIndex(10, 10, 600));
    }

    [Fact]
    public void MiddleBar_NearCenter()
    {
        Assert.Equal(5, HistoryBarHoverResolver.ResolveIndex(10, 300, 600));
    }

    [Fact]
    public void LastBar_NearRightEdge()
    {
        Assert.Equal(9, HistoryBarHoverResolver.ResolveIndex(10, 599, 600));
    }

    [Theory]
    [InlineData(-500.0, 0)]
    [InlineData(5000.0, 9)]
    public void PointerBeyondPlot_ClampsToFirstOrLast(double pointerX, int expected)
    {
        Assert.Equal(expected, HistoryBarHoverResolver.ResolveIndex(10, pointerX, 600));
    }
}
