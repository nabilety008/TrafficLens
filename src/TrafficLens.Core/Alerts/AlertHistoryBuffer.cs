namespace TrafficLens.Core.Alerts;

/// <summary>
/// Session-only, bounded, thread-safe history of recent alerts (newest first).
/// Never persisted; overflow trims the oldest entry. Capacity defaults to 100.
/// </summary>
public sealed class AlertHistoryBuffer
{
    private readonly object _gate = new();
    private readonly Queue<AlertEvent> _events = new();
    private readonly int _capacity;

    public AlertHistoryBuffer(int capacity = 100)
    {
        _capacity = Math.Max(1, capacity);
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _events.Count;
            }
        }
    }

    public void Add(AlertEvent alert)
    {
        lock (_gate)
        {
            _events.Enqueue(alert);
            while (_events.Count > _capacity)
            {
                _events.Dequeue();
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _events.Clear();
        }
    }

    /// <summary>Newest-first snapshot safe to bind or iterate.</summary>
    public IReadOnlyList<AlertEvent> Latest()
    {
        lock (_gate)
        {
            return _events.Reverse().ToArray();
        }
    }
}