using System.Windows;

namespace TrafficLens.App.Services;

/// <summary>
/// Clamps a widget position so the window rectangle stays within the union of
/// the given monitor work areas. Handles negative virtual-screen coordinates
/// (secondary monitor left of primary) and monitors that were disconnected
/// between runs. Pure and unit-testable; the WPF work-area lookup lives in
/// <see cref="FloatingWidgetService"/>.
/// </summary>
public static class WidgetPositionHelper
{
    public static (double Left, double Top) Clamp(
        double left,
        double top,
        double width,
        double height,
        IReadOnlyList<Rect> workAreas)
    {
        if (workAreas.Count == 0)
        {
            return (0, 0);
        }

        double minLeft = workAreas.Min(r => r.Left);
        double maxRight = workAreas.Max(r => r.Left + r.Width);
        double minTop = workAreas.Min(r => r.Top);
        double maxBottom = workAreas.Max(r => r.Top + r.Height);

        double clampedLeft = Math.Max(minLeft, Math.Min(left, maxRight - width));
        double clampedTop = Math.Max(minTop, Math.Min(top, maxBottom - height));

        if (maxRight - minLeft < width)
        {
            clampedLeft = minLeft;
        }

        if (maxBottom - minTop < height)
        {
            clampedTop = minTop;
        }

        return (clampedLeft, clampedTop);
    }
}