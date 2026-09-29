namespace TrafficLens.Core.Graph;

/// <summary>
/// Compact Y-axis reference levels derived from the adaptive scale maximum.
/// This is presentation math only — it never changes what the graph draws or
/// how <see cref="AdaptiveGraphScale"/> picks its maximum; the labels are read
/// from the same maximum the drawing uses, so they always agree with the plot.
/// </summary>
public static class GraphScaleLabels
{
    /// <summary>
    /// Returns the reference levels (bottom → top) for a scale maximum.
    /// Always three levels: 0, the mid point, and the maximum itself.
    /// Pure and allocation-light; used by the graph view for axis labels.
    /// </summary>
    public static IReadOnlyList<GraphScaleLevel> Levels(long scaleMax)
    {
        var max = Math.Max(scaleMax, 1);
        var half = max / 2;

        return new[]
        {
            new GraphScaleLevel(Position.Bottom, 0),
            new GraphScaleLevel(Position.Middle, half),
            new GraphScaleLevel(Position.Top, max)
        };
    }
}

public enum Position
{
    Bottom,
    Middle,
    Top
}

public readonly record struct GraphScaleLevel(Position Position, long BytesPerSecond);
