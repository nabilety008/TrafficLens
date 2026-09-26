using TrafficLens.WinUI.Services;

namespace TrafficLens.WinUI.Tests;

public class WidgetPositionHelperTests
{
    [Fact]
    public void GetWorkAreas_EnumeratesDisplayAreasWithoutThrowing()
    {
        var areas = WidgetPositionHelper.GetWorkAreas();

        Assert.NotEmpty(areas);
        Assert.All(areas, area => Assert.True(area.Width > 0 && area.Height > 0));
    }

    [Fact]
    public void Clamp_FarOutsideAnyWorkArea_ClampsToFirstWorkAreaOrigin()
    {
        var first = WidgetPositionHelper.GetWorkAreas()[0];

        var (left, top) = WidgetPositionHelper.Clamp(-99999, -99999, 1, 1);

        Assert.Equal(first.X, left);
        Assert.Equal(first.Y, top);
    }

    [Fact]
    public void Clamp_InsideFirstWorkArea_KeepsPosition()
    {
        var first = WidgetPositionHelper.GetWorkAreas()[0];
        var left = first.X + 10;
        var top = first.Y + 10;

        var (clampedLeft, clampedTop) = WidgetPositionHelper.Clamp(left, top, 1, 1);

        Assert.Equal(left, clampedLeft);
        Assert.Equal(top, clampedTop);
    }
}
