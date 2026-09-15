using TrafficLens.Network.Process;

namespace TrafficLens.Network.Connections;

/// <summary>
/// Bounded, short-TTL cache over <see cref="IProcessMetadataProvider"/> for the
/// connection poll loop. Each poll can touch hundreds of distinct PIDs (UDP
/// rows especially); querying <see cref="System.Diagnostics.Process"/> for all
/// of them every second would be wasteful. Negative results (process gone) are
/// cached too so dead PIDs do not trigger a lookup every tick. Identity is the
/// full <see cref="ProcessInstanceId"/> (PID + start time) so PID reuse still
/// resolves to the correct instance. Never throws; an underlying failure
/// surfaces as a non-existent process.
/// </summary>
public sealed class ConnectionProcessResolver
{
    private sealed record CacheEntry(
        ProcessMetadata? Metadata,
        bool ProcessExists,
        long ExpiresAtUtcTicks);

    private readonly IProcessMetadataProvider _inner;
    private readonly TimeSpan _ttl;
    private readonly int _capacity;
    private readonly Dictionary<ProcessInstanceId, CacheEntry> _cache = [];
    private readonly Queue<ProcessInstanceId> _order = new();
    private readonly object _gate = new();

    public ConnectionProcessResolver(IProcessMetadataProvider inner)
        : this(inner, TimeSpan.FromSeconds(3), 512)
    {
    }

    internal ConnectionProcessResolver(IProcessMetadataProvider inner, TimeSpan ttl, int capacity)
    {
        _inner = inner;
        _ttl = ttl;
        _capacity = capacity;
    }

    /// <summary>
    /// Resolves a PID to process metadata, respecting start-time identity when
    /// it is available. Returns (Metadata=null, ProcessExists=false) for unknown
    /// processes. Safe to call from the polling loop; never throws.
    /// </summary>
    public (ProcessMetadata? Metadata, bool ProcessExists) Resolve(int processId, long startTimeUtcTicks)
    {
        var identity = new ProcessInstanceId(processId, startTimeUtcTicks);
        var now = DateTime.UtcNow.Ticks;
        lock (_gate)
        {
            if (_cache.TryGetValue(identity, out var entry))
            {
                if (entry.ExpiresAtUtcTicks > now)
                {
                    return (entry.Metadata, entry.ProcessExists);
                }

                _cache.Remove(identity);
            }

            var result = _inner.Resolve(identity);
            Add(identity, result, now + _ttl.Ticks);
            return (result.Metadata, result.ProcessExists);
        }
    }

    private void Add(ProcessInstanceId identity, ProcessMetadataResult result, long expiresAt)
    {
        if (_cache.ContainsKey(identity))
        {
            _cache[identity] = new CacheEntry(result.Metadata, result.ProcessExists, expiresAt);
            return;
        }

        while (_cache.Count >= _capacity && _order.Count > 0)
        {
            _cache.Remove(_order.Dequeue());
        }

        _cache[identity] = new CacheEntry(result.Metadata, result.ProcessExists, expiresAt);
        _order.Enqueue(identity);
    }
}