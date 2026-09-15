using TrafficLens.Core.Models;

namespace TrafficLens.Core.History;

/// <summary>
/// Converts the live per-adapter cumulative counters that TrafficLens already
/// samples into compact, durable minute buckets of SYSTEM/GLOBAL traffic.
///
/// Semantics (mirrors the existing speed-rate / aggregate pipeline):
/// <list type="bullet">
/// <item>The first observation of an adapter only establishes a baseline
/// (no delta).</item>
/// <item>Only non-negative counter deltas are accumulated.</item>
/// <item>A counter decrease (reset / wrap / NIC replacement) re-baselines and
/// emits no delta — never a giant fake value.</item>
/// <item>An adapter that disappears simply stops contributing; a reconnecting
/// adapter either continues (real delta) or re-baselines (reset).</item>
/// <item>Tunnel/VPN adapters are excluded by default, matching ADR-009/010's
/// non-overlapping system-total policy (no double counting).</item>
/// <item>Deltas from all eligible adapters in a minute are summed into one
/// bucket so we persist roughly one row per minute, never per event.</item>
/// </list>
///
/// This type is pure and thread-safe; it performs no I/O and never touches
/// SQLite or the UI.
/// </summary>
public sealed class TrafficHistoryAccumulator
{
    public const int BucketSeconds = 60;

    private readonly bool _includeTunnels;
    private readonly Dictionary<string, CounterBaseline> _baselines =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SortedDictionary<DateTime, MutableBucket> _buckets = new();
    private readonly object _sync = new();

    public TrafficHistoryAccumulator(bool includeTunnels = false)
    {
        _includeTunnels = includeTunnels;
    }

    public int BaselineCount
    {
        get
        {
            lock (_sync)
            {
                return _baselines.Count;
            }
        }
    }

    public int PendingBucketCount
    {
        get
        {
            lock (_sync)
            {
                return _buckets.Count;
            }
        }
    }

    /// <summary>
    /// Feeds one cumulative counter observation. <paramref name="kind"/> is the
    /// adapter kind used for the tunnel policy; a null kind is treated as
    /// non-tunnel (counted) so a lagging metadata provider never silently drops
    /// real traffic.
    /// </summary>
    public void Ingest(NetworkCounterSample sample, NetworkAdapterKind? kind)
    {
        if (!_includeTunnels && kind == NetworkAdapterKind.Tunnel)
        {
            return;
        }

        lock (_sync)
        {
            var hasDelta = _baselines.TryGetValue(sample.AdapterId, out var baseline)
                && sample.ReceivedBytes >= baseline.ReceivedBytes
                && sample.SentBytes >= baseline.SentBytes;

            var download = hasDelta ? sample.ReceivedBytes - baseline.ReceivedBytes : 0;
            var upload = hasDelta ? sample.SentBytes - baseline.SentBytes : 0;

            _baselines[sample.AdapterId] = new CounterBaseline(
                Math.Max(sample.ReceivedBytes, 0),
                Math.Max(sample.SentBytes, 0));

            if (download <= 0 && upload <= 0)
            {
                return;
            }

            var bucketStart = FloorToMinute(sample.Timestamp);
            if (_buckets.TryGetValue(bucketStart, out var existing))
            {
                existing.DownloadBytes += download;
                existing.UploadBytes += upload;
            }
            else
            {
                _buckets[bucketStart] = new MutableBucket
                {
                    DownloadBytes = download,
                    UploadBytes = upload
                };
            }
        }
    }

    /// <summary>
    /// Removes and returns all buckets strictly older than the minute containing
    /// <paramref name="utcNow"/>. Those buckets are complete 60-second buckets.
    /// </summary>
    public IReadOnlyList<TrafficHistoryBucket> DrainCompleted(DateTime utcNow)
    {
        var cutoff = FloorToMinute(utcNow);
        return Drain(bucketStart => bucketStart < cutoff, cutoff, utcNow);
    }

    /// <summary>
    /// Removes and returns every pending bucket, marking the still-open minute
    /// with its real (shorter) duration. Used on graceful shutdown so at most the
    /// current partial minute would be lost on a crash.
    /// </summary>
    public IReadOnlyList<TrafficHistoryBucket> DrainAll(DateTime utcNow)
    {
        var cutoff = FloorToMinute(utcNow);
        return Drain(_ => true, cutoff, utcNow);
    }

    private IReadOnlyList<TrafficHistoryBucket> Drain(
        Func<DateTime, bool> predicate,
        DateTime openMinute,
        DateTime utcNow)
    {
        lock (_sync)
        {
            if (_buckets.Count == 0)
            {
                return Array.Empty<TrafficHistoryBucket>();
            }

            var result = new List<TrafficHistoryBucket>();
            foreach (var bucketStart in _buckets.Keys.Where(predicate).ToList())
            {
                var bucket = _buckets[bucketStart];
                _buckets.Remove(bucketStart);
                result.Add(new TrafficHistoryBucket(
                    bucketStart,
                    DurationSeconds(bucketStart, openMinute, utcNow),
                    bucket.DownloadBytes,
                    bucket.UploadBytes));
            }

            return result;
        }
    }

    private static int DurationSeconds(DateTime bucketStart, DateTime openMinute, DateTime utcNow)
    {
        if (bucketStart < openMinute)
        {
            return BucketSeconds;
        }

        var elapsed = (utcNow - bucketStart).TotalSeconds;
        var rounded = (int)Math.Ceiling(elapsed);
        return Math.Clamp(rounded, 1, BucketSeconds);
    }

    private static DateTime FloorToMinute(DateTime timestamp) => new(
        timestamp.Year,
        timestamp.Month,
        timestamp.Day,
        timestamp.Hour,
        timestamp.Minute,
        0,
        DateTimeKind.Utc);

    private struct CounterBaseline(long ReceivedBytes, long SentBytes)
    {
        public long ReceivedBytes { get; } = ReceivedBytes;

        public long SentBytes { get; } = SentBytes;
    }

    private sealed class MutableBucket
    {
        public long DownloadBytes { get; set; }

        public long UploadBytes { get; set; }
    }
}
