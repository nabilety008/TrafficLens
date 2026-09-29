namespace TrafficLens.Core.Graph;

/// <summary>
/// Pure nearest-sample resolution for the live traffic graph hover. Operates on
/// the already-displayed slice only; no timers, no I/O, no interpolation of
/// measurements that were not measured.
/// </summary>
public static class GraphHoverResolver
{
    /// <summary>
    /// Maps a pointer X (in plot coordinates, 0 = plot left edge) to the nearest
    /// sample, using the same time→X mapping the view draws with:
    /// x = (point.Timestamp - start).TotalSeconds / windowSeconds * plotWidth.
    /// Clamps the result to the first/last sample at the graph edges.
    /// Returns null for an empty buffer.
    /// </summary>
    public static GraphHoverSample? Resolve(
        IReadOnlyList<TrafficGraphPoint> points,
        double pointerX,
        double plotWidth,
        double windowSeconds,
        DateTime referenceTime)
    {
        if (points.Count == 0 || plotWidth <= 0)
        {
            return null;
        }

        var window = Math.Max(windowSeconds, 1);
        var start = referenceTime - TimeSpan.FromSeconds(window);

        var bestIndex = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < points.Count; i++)
        {
            var elapsed = (points[i].Timestamp - start).TotalSeconds;
            if (elapsed < 0)
            {
                continue;
            }

            var x = elapsed / window * plotWidth;
            var distance = Math.Abs(x - pointerX);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        var chosen = points[bestIndex];
        return new GraphHoverSample(
            chosen.Timestamp,
            chosen.DownloadBytesPerSecond,
            chosen.UploadBytesPerSecond);
    }
}

public readonly record struct GraphHoverSample(
    DateTime Timestamp,
    long DownloadBytesPerSecond,
    long UploadBytesPerSecond);
