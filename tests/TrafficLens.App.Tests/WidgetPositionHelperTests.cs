using System.Windows;
using TrafficLens.App.Services;

namespace TrafficLens.App.Tests;

public class WidgetPositionHelperTests
{
    private static Rect WorkArea(double left, double top, double right, double bottom) =>
        new(left, top, right - left, bottom - top);

    private static (double Left, double Top) Clamp(
        double left,
        double top,
        double width,
        double height,
        params Rect[] areas) =>
        WidgetPositionHelper.Clamp(left, top, width, height, areas);

    [Fact]
    public void Clamp_InsideWorkArea_Unchanged()
    {
        var (left, top) = Clamp(100, 200, 280, 110, WorkArea(0, 0, 1920, 1080));

        Assert.Equal(100, left);
        Assert.Equal(200, top);
    }

    [Fact]
    public void Clamp_OffScreenRight_ClampedToRightEdge()
    {
        var (left, top) = Clamp(1800, 200, 280, 110, WorkArea(0, 0, 1920, 1080));

        Assert.Equal(1640, left);
        Assert.Equal(200, top);
    }

    [Fact]
    public void Clamp_OffScreenBottom_ClampedToBottomEdge()
    {
        var (left, top) = Clamp(100, 1050, 280, 110, WorkArea(0, 0, 1920, 1080));

        Assert.Equal(100, left);
        Assert.Equal(970, top);
    }

    [Fact]
    public void Clamp_NegativeCoordinates_ClampedToVisibleArea()
    {
        var (left, top) = Clamp(-300, -50, 280, 110, WorkArea(0, 0, 1920, 1080));

        Assert.Equal(0, left);
        Assert.Equal(0, top);
    }

    [Fact]
    public void Clamp_SecondaryMonitorLeftOfPrimary_KeepsVisibleNegativeCoords()
    {
        var (left, top) = Clamp(-1800, 200, 280, 110, WorkArea(-1920, 0, 0, 1080));

        Assert.Equal(-1800, left);
        Assert.Equal(200, top);
    }

    [Fact]
    public void Clamp_SecondaryMonitorLeft_ClampedWhenOffScreen()
    {
        var (left, top) = Clamp(-2000, 200, 280, 110, WorkArea(-1920, 0, 0, 1080));

        Assert.Equal(-1920, left);
        Assert.Equal(200, top);
    }

    [Fact]
    public void Clamp_MultiMonitorUnion_ClampsToUnionBounds()
    {
        var areas = new[]
        {
            WorkArea(-1920, 0, 0, 1080),
            WorkArea(0, -150, 1920, 930)
        };

        var (left, top) = Clamp(1900, -50, 280, 110, areas);

        Assert.Equal(1640, left);
        Assert.Equal(-50, top);
    }

    [Fact]
    public void Clamp_MultiMonitorUnion_ClampsToNearestVisibleEdge()
    {
        var areas = new[]
        {
            WorkArea(-1920, 0, 0, 1080),
            WorkArea(0, -150, 1920, 930)
        };

        var (left, top) = Clamp(3000, -300, 280, 110, areas);

        Assert.Equal(1640, left);
        Assert.Equal(-150, top);
    }

    [Fact]
    public void Clamp_EmptyWorkAreas_ReturnsZero()
    {
        var (left, top) = WidgetPositionHelper.Clamp(100, 200, 280, 110, Array.Empty<Rect>());

        Assert.Equal(0, left);
        Assert.Equal(0, top);
    }

    [Fact]
    public void Clamp_WindowLargerThanWorkArea_ClampedToTopLeft()
    {
        var (left, top) = Clamp(100, 200, 1000, 800, WorkArea(0, 0, 800, 600));

        Assert.Equal(0, left);
        Assert.Equal(0, top);
    }
}