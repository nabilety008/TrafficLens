using TrafficLens.WinUI.Services;
using Windows.Foundation;

namespace TrafficLens.WinUI.Tests;

/// <summary>
/// Movement bounds for the Floating Widget. All values are physical pixels, the
/// same space AppWindow.Position and AppWindow.Size use.
/// </summary>
public class WidgetPositionClampTests
{
    private static readonly Rect Primary = new(0, 0, 1920, 1040);

    private static readonly Rect SecondaryRight = new(1920, 0, 2560, 1400);

    private static readonly Rect SecondaryLeftNegative = new(-2560, -200, 2560, 1400);

    private static IReadOnlyList<Rect> One(params Rect[] areas) => areas;

    [Fact]
    public void Left_ClampsToWorkAreaLeft()
    {
        var (left, _) = WidgetPositionHelper.Clamp(-500, 300, 340, 140, One(Primary));

        Assert.Equal(Primary.X, left);
    }

    [Fact]
    public void Right_ClampsSoWholeWidgetFits()
    {
        var (left, _) = WidgetPositionHelper.Clamp(5000, 300, 340, 140, One(Primary));

        Assert.Equal(Primary.X + Primary.Width - 340, left);
    }

    [Fact]
    public void Top_ClampsToWorkAreaTop()
    {
        var (_, top) = WidgetPositionHelper.Clamp(300, -900, 340, 140, One(Primary));

        Assert.Equal(Primary.Y, top);
    }

    [Fact]
    public void Bottom_ClampsSoWholeWidgetFits()
    {
        var (_, top) = WidgetPositionHelper.Clamp(300, 5000, 340, 140, One(Primary));

        Assert.Equal(Primary.Y + Primary.Height - 140, top);
    }

    [Fact]
    public void ValidPosition_IsUnchanged()
    {
        var (left, top) = WidgetPositionHelper.Clamp(640, 480, 340, 140, One(Primary));

        Assert.Equal(640, left);
        Assert.Equal(480, top);
    }

    [Fact]
    public void NonZeroWorkAreaOrigin_IsRespected()
    {
        var area = new Rect(1920, 40, 1280, 1000);

        var (left, top) = WidgetPositionHelper.Clamp(2000, 200, 340, 140, One(area));

        Assert.Equal(2000, left);
        Assert.Equal(200, top);
    }

    [Fact]
    public void NonZeroWorkAreaOrigin_ClampsToItsOwnEdges()
    {
        var area = new Rect(1920, 40, 1280, 1000);

        var (left, top) = WidgetPositionHelper.Clamp(0, 0, 340, 140, One(area));

        Assert.Equal(1920, left);
        Assert.Equal(40, top);
    }

    [Fact]
    public void NegativeMonitorOrigin_IsRespected()
    {
        var (left, top) = WidgetPositionHelper.Clamp(-2000, 100, 340, 140, One(SecondaryLeftNegative));

        Assert.Equal(-2000, left);
        Assert.Equal(100, top);
    }

    [Fact]
    public void NegativeMonitorOrigin_ClampsToItsNegativeEdges()
    {
        var (_, top) = WidgetPositionHelper.Clamp(-99999, -99999, 340, 140, One(SecondaryLeftNegative));

        Assert.Equal(SecondaryLeftNegative.Y, top);
        Assert.Equal(SecondaryLeftNegative.Y, top);
    }

    [Fact]
    public void CrossMonitorDrag_UsesTheDestinationMonitorWorkArea()
    {
        var areas = One(Primary, SecondaryRight);

        // Dragged past the right edge of the second monitor, so it must come back
        // to that monitor's right edge and not jump back to the first one.
        var (left, _) = WidgetPositionHelper.Clamp(4400, 300, 340, 140, areas);

        Assert.Equal(SecondaryRight.X + SecondaryRight.Width - 340, left);
    }

    [Fact]
    public void PositionInsideSecondaryMonitor_IsLeftAlone()
    {
        var areas = One(Primary, SecondaryRight);

        // Already fully within the second monitor: the clamp must not touch it.
        var (left, _) = WidgetPositionHelper.Clamp(4000, 300, 340, 140, areas);

        Assert.Equal(4000, left);
    }

    [Fact]
    public void CrossMonitorDrag_BackToPrimary_UsesPrimaryWorkArea()
    {
        var areas = One(Primary, SecondaryRight);

        // Dragged back onto the primary monitor. The position is already valid, and
        // critically the clamp must not push it across to the second monitor.
        var (left, _) = WidgetPositionHelper.Clamp(100, 300, 340, 140, areas);

        Assert.Equal(100, left);
    }

    [Fact]
    public void PositionInsidePrimary_IsNotPushedToTheOtherMonitor()
    {
        var areas = One(Primary, SecondaryRight);

        // Regression guard: a position overlapping the primary must select the
        // primary work area even though a later candidate is nearer by centre.
        var area = WidgetPositionHelper.SelectWorkArea(100, 300, 340, 140, areas);

        Assert.Equal(Primary, area);
    }

    [Fact]
    public void DisconnectedMonitor_RecoversIntoTheRemainingMonitor()
    {
        // Saved on a monitor that is gone; only the primary is left.
        var (left, top) = WidgetPositionHelper.Clamp(3000, 700, 340, 140, One(Primary));

        Assert.InRange(left, Primary.X, Primary.X + Primary.Width - 340);
        Assert.InRange(top, Primary.Y, Primary.Y + Primary.Height - 140);
    }

    [Fact]
    public void DisconnectedMonitor_RecoversIntoTheNearestRemainingMonitor()
    {
        // Two monitors remain, neither is the disconnected one. The nearest by centre
        // is the left-hand monitor, so the widget must come back there.
        var areas = One(Primary, SecondaryLeftNegative);

        var (left, _) = WidgetPositionHelper.Clamp(-2500, 500, 340, 140, areas);

        Assert.InRange(left, SecondaryLeftNegative.X, SecondaryLeftNegative.X + SecondaryLeftNegative.Width - 340);
    }

    [Fact]
    public void OffScreenSavedPosition_RecoversIntoAWorkArea()
    {
        var (left, top) = WidgetPositionHelper.Clamp(-99999, -99999, 340, 140, One(Primary));

        Assert.Equal(Primary.X, left);
        Assert.Equal(Primary.Y, top);
    }

    [Fact]
    public void TaskbarWorkArea_IsHonoured()
    {
        // 1040 tall display with a 40px taskbar at the bottom.
        var workArea = new Rect(0, 0, 1920, 1000);

        var (_, top) = WidgetPositionHelper.Clamp(300, 4000, 340, 140, One(workArea));

        Assert.Equal(workArea.Y + workArea.Height - 140, top);
    }

    [Fact]
    public void WidgetLargerThanWorkArea_IsHandledSafely()
    {
        var tiny = new Rect(0, 0, 200, 100);

        var (left, top) = WidgetPositionHelper.Clamp(900, 900, 340, 140, One(tiny));

        Assert.Equal(tiny.X, left);
        Assert.Equal(tiny.Y, top);
    }

    [Fact]
    public void DpiScaledWidgetSize_IsUsedInTheClamp()
    {
        // 340x140 DIP at 150% is 510x210 physical pixels.
        var (left, top) = WidgetPositionHelper.Clamp(5000, 5000, 510, 210, One(Primary));

        Assert.Equal(Primary.X + Primary.Width - 510, left);
        Assert.Equal(Primary.Y + Primary.Height - 210, top);
    }

    [Fact]
    public void DpiScaledWidgetSize_AtTwoHundredPercent()
    {
        var (left, top) = WidgetPositionHelper.Clamp(5000, 5000, 680, 280, One(Primary));

        Assert.Equal(Primary.X + Primary.Width - 680, left);
        Assert.Equal(Primary.Y + Primary.Height - 280, top);
    }

    [Fact]
    public void NoWorkAreas_LeavesPositionUnchanged()
    {
        var (left, top) = WidgetPositionHelper.Clamp(123, 456, 340, 140, One());

        Assert.Equal(123, left);
        Assert.Equal(456, top);
    }

    [Fact]
    public void SelectWorkArea_PrefersTheMonitorWithLargestOverlap()
    {
        var areas = One(Primary, SecondaryRight);

        var area = WidgetPositionHelper.SelectWorkArea(2000, 300, 340, 140, areas);

        Assert.Equal(SecondaryRight, area);
    }
}
