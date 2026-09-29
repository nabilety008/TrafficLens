namespace TrafficLens.Core.Graph;

/// <summary>
/// Pure nearest-bar selection for the history bar chart hover. Uses the same
/// slot math the chart draws with (center of slot i at (i + 0.5) * slotWidth).
/// Operates on the already-loaded series only — never queries storage.
/// </summary>
public static class HistoryBarHoverResolver
{
    /// <summary>
    /// Returns the index of the nearest bar for a pointer X in plot
    /// coordinates (0 = plot left edge), or null when the series is empty.
    /// The pointer is clamped into the plot range, so edges resolve to the
    /// first/last bar.
    /// </summary>
    public static int? ResolveIndex(int pointCount, double pointerX, double plotWidth)
    {
        if (pointCount <= 0 || plotWidth <= 0)
        {
            return null;
        }

        var clamped = Math.Clamp(pointerX, 0, plotWidth);
        var slot = plotWidth / pointCount;
        var index = (int)(clamped / slot);
        return Math.Clamp(index, 0, pointCount - 1);
    }
}
