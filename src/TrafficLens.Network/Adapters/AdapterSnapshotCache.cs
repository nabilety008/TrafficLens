namespace TrafficLens.Network.Adapters;

/// <summary>
/// Thread-safe TTL cache that collapses many adapter snapshot requests within a
/// refresh window into a single underlying enumeration. The collector loop, the
/// dashboard and the alert pipeline each pull the adapter snapshot once per
/// speed sample (per adapter, per second); without this dedupe the app triggers
/// a full native interface enumeration (NetworkInterface.GetAllNetworkInterfaces
/// plus per-interface IP statistics) several times per second. A full
/// enumeration is comparatively expensive (kernel IP Helper queries), which on
/// machines with many adapters can burn a large share of a CPU core while idle.
/// <see cref="Invalidate"/> makes the next call re-enumerate immediately and is
/// wired to NetworkChange events so topology changes are still observed promptly.
/// A failed enumeration is never cached: the failure propagates to the caller
/// and the next call retries the underlying source.
/// </summary>
public sealed class AdapterSnapshotCache
{
    private readonly Func<IReadOnlyList<RawAdapterSnapshot>> _loader;
    private readonly TimeSpan _refreshInterval;
    private readonly TimeProvider _time;
    private readonly object _gate = new();

    private IReadOnlyList<RawAdapterSnapshot>? _snapshot;
    private DateTimeOffset _lastEnumeratedUtc = DateTimeOffset.MinValue;
    private bool _invalidated = true;

    public AdapterSnapshotCache(
        Func<IReadOnlyList<RawAdapterSnapshot>> loader,
        TimeSpan? refreshInterval = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _loader = loader;
        _refreshInterval = refreshInterval ?? TimeSpan.FromSeconds(1);
        _time = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlyList<RawAdapterSnapshot> GetSnapshot()
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            if (_snapshot is not null && !_invalidated && now - _lastEnumeratedUtc < _refreshInterval)
            {
                return _snapshot;
            }

            _snapshot = _loader();
            _lastEnumeratedUtc = now;
            _invalidated = false;
            return _snapshot;
        }
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _invalidated = true;
        }
    }
}