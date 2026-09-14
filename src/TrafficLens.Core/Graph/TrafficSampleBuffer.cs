namespace TrafficLens.Core.Graph;

/// <summary>
/// Bounded, thread-safe time-series ring buffer for live graph samples.
///
/// - Fixed capacity: memory never grows (oldest samples are dropped at capacity).
/// - Time-based retention: samples older than <see cref="MaxRetention"/> are pruned
///   by wall-clock timestamp, not by sample count.
/// - Duplicate/out-of-order timestamps are rejected so one poll cycle appends a
///   single sample regardless of how many per-adapter events need marshalling.
/// - Preserves real sample timestamps; X-axis mapping down the line uses actual
///   elapsed time, never an assumed fixed interval.
/// </summary>
public sealed class TrafficSampleBuffer
{
    private readonly object _sync = new();
    private readonly TrafficGraphPoint[] _items;
    private readonly TimeSpan _maxRetention;
    private int _head;
    private int _count;

    public TrafficSampleBuffer(TimeSpan maxRetention, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(capacity, 0);
        _maxRetention = maxRetention;
        _items = new TrafficGraphPoint[capacity];
    }

    public int Capacity => _items.Length;

    public TimeSpan MaxRetention => _maxRetention;

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _count;
            }
        }
    }

    public DateTime? LatestTimestamp
    {
        get
        {
            lock (_sync)
            {
                return _count == 0 ? null : _items[LastIndex].Timestamp;
            }
        }
    }

    public bool Add(DateTime timestamp, long downloadBytesPerSecond, long uploadBytesPerSecond) =>
        Add(new TrafficGraphPoint(timestamp, downloadBytesPerSecond, uploadBytesPerSecond));

    public bool Add(TrafficGraphPoint point)
    {
        lock (_sync)
        {
            if (_count > 0 && point.Timestamp <= _items[LastIndex].Timestamp)
            {
                return false;
            }

            var pos = _count == 0 ? 0 : (LastIndex + 1) % _items.Length;
            _items[pos] = point;

            if (_count == _items.Length)
            {
                _head = (_head + 1) % _items.Length;
            }
            else
            {
                _count++;
            }

            PruneOlderThan(point.Timestamp - _maxRetention);
            return true;
        }
    }

    /// <summary>
    /// Returns the newest samples at or after <paramref name="now"/> - <paramref name="window"/>,
    /// in chronological order. Does not mutate the buffer, so range switching
    /// never discards history.
    /// </summary>
    public IReadOnlyList<TrafficGraphPoint> Slice(TimeSpan window, DateTime now)
    {
        lock (_sync)
        {
            if (_count == 0)
            {
                return Array.Empty<TrafficGraphPoint>();
            }

            var cutoff = now - window;
            var result = new List<TrafficGraphPoint>(Math.Min(_count, (int)(window.TotalSeconds * 4) + 16));

            for (var i = 0; i < _count; i++)
            {
                var item = _items[(_head + i) % _items.Length];
                if (item.Timestamp >= cutoff)
                {
                    result.Add(item);
                }
            }

            return result;
        }
    }

    private int LastIndex => (_head + _count - 1) % _items.Length;

    private void PruneOlderThan(DateTime cutoff)
    {
        while (_count > 0 && _items[_head].Timestamp < cutoff)
        {
            _head = (_head + 1) % _items.Length;
            _count--;
        }
    }
}