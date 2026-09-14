namespace TrafficLens.Core.Graph;

/// <summary>
/// Adaptive Y-axis maximum (bytes/second) for the traffic graph. Keeps the two
/// series on a single shared, meaningful scale:
///
/// - Growth is immediate, so sudden spikes always stay visible.
/// - Shrink is hysteretic: the scale only lowers after traffic has stayed below
///   a sustained fraction of the current maximum for consecutive updates,
///   preventing visual flicker from tiny short-lived dips.
/// - A floor guarantees a non-zero denominator even when every sample is zero
///   (no divide-by-zero; the graph renders a flat baseline).
///
/// The scale is presentation-only: it never touches the measured sample values.
/// The hysteresis rule is documented in docs/DECISIONS.md (ADR-012).
/// </summary>
public sealed class AdaptiveGraphScale
{
    public const long FloorBytesPerSecond = 2048;

    private const double ShrinkWhenPeakBelowFraction = 0.35;
    private const int ConsecutiveLowUpdatesRequired = 2;

    private long _current;
    private int _consecutiveLow;

    public AdaptiveGraphScale() : this(FloorBytesPerSecond)
    {
    }

    public AdaptiveGraphScale(long initialMaxBytesPerSecond)
    {
        _current = Math.Max(1, initialMaxBytesPerSecond);
    }

    public long Current => _current;

    public int LowConsecutiveCount => _consecutiveLow;

    /// <summary>
    /// Updates the scale from the windowed points (both series combined) and
    /// returns the proposed Y-axis maximum. Pure rule, unit-testable.
    /// </summary>
    public long Update(IReadOnlyList<TrafficGraphPoint> points)
    {
        var peak = 0L;
        foreach (var p in points)
        {
            if (p.DownloadBytesPerSecond > peak)
            {
                peak = p.DownloadBytesPerSecond;
            }

            if (p.UploadBytesPerSecond > peak)
            {
                peak = p.UploadBytesPerSecond;
            }
        }

        if (points.Count == 0)
        {
            _consecutiveLow = 0;
            _current = Math.Max(1, FloorBytesPerSecond);
            return _current;
        }

        if (peak >= _current)
        {
            _consecutiveLow = 0;
            _current = NiceCeil(Math.Max(peak, 1));
            return _current;
        }

        if (peak <= _current * ShrinkWhenPeakBelowFraction)
        {
            _consecutiveLow++;
            if (_consecutiveLow >= ConsecutiveLowUpdatesRequired)
            {
                _current = NiceCeil(Math.Max(peak, FloorBytesPerSecond));
                _consecutiveLow = 0;
            }
        }
        else
        {
            _consecutiveLow = 0;
        }

        return _current;
    }

    public void Reset()
    {
        _current = Math.Max(1, FloorBytesPerSecond);
        _consecutiveLow = 0;
    }

    /// <summary>
    /// Round a positive value up to a "nice" number of the form k * 10^n with
    /// k in {1, 2, 5} so Y-axis labels are readable (0.5 KB/s, 2 MB/s, ...).
    /// </summary>
    public static long NiceCeil(long value)
    {
        var magnitude = 1L;
        while (value > magnitude * 10)
        {
            magnitude *= 10;
        }

        foreach (var factor in new[] { 1L, 2L, 5L, 10L })
        {
            var candidate = factor * magnitude;
            if (candidate >= value)
            {
                return candidate;
            }
        }

        return 10 * magnitude;
    }
}