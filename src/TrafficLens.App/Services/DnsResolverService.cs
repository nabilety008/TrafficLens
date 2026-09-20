using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace TrafficLens.App.Services;

/// <summary>
/// Bounded reverse DNS resolver with TTL cache, concurrency limiting, failure
/// caching, and cooperative cancellation. Designed for UI-friendly background
/// resolution of remote IP addresses in the Connections page.
/// </summary>
public sealed class DnsResolverService : IDisposable
{
    private readonly ILogger<DnsResolverService> _logger;
    private readonly SemaphoreSlim _concurrencyGate;
    private readonly TimeSpan _successTtl;
    private readonly TimeSpan _failureTtl;
    private readonly int _maxCacheSize;
    private readonly ConcurrentDictionary<IPAddress, DnsCacheEntry> _cache = new();
    private readonly ConcurrentDictionary<IPAddress, Task<string?>> _inFlight = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public DnsResolverService(
        ILogger<DnsResolverService> logger,
        int maxConcurrentLookups = 4,
        TimeSpan? successTtl = null,
        TimeSpan? failureTtl = null,
        int maxCacheSize = 1024)
    {
        _logger = logger;
        _concurrencyGate = new SemaphoreSlim(maxConcurrentLookups, maxConcurrentLookups);
        _successTtl = successTtl ?? TimeSpan.FromMinutes(30);
        _failureTtl = failureTtl ?? TimeSpan.FromMinutes(5);
        _maxCacheSize = maxCacheSize;
    }

    /// <summary>
    /// Gets the cached hostname for an IP, or starts a background lookup if not cached.
    /// Returns the cached value immediately if available (including negative cache),
    /// otherwise returns null and begins an async lookup that will update the
    /// ConnectionRowViewModel via callback when complete.
    /// </summary>
    public string? GetOrResolve(IPAddress address, Action<string?>? onResolved = null)
    {
        if (_disposed || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.Loopback) || address.Equals(IPAddress.IPv6Loopback))
        {
            return null;
        }

        // Check cache first
        if (_cache.TryGetValue(address, out var entry) && !entry.IsExpired)
        {
            return entry.Hostname;
        }

        // Start or get in-flight lookup
        var lookupTask = _inFlight.GetOrAdd(address, _ => StartLookup(address));

        // Attach continuation to update callback when complete (fire-and-forget)
        _ = lookupTask.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && onResolved is not null)
            {
                onResolved(t.Result);
            }
        }, TaskContinuationOptions.OnlyOnRanToCompletion);

        return entry?.Hostname;
    }

    /// <summary>
    /// Starts a DNS lookup for the given address with concurrency limiting and caching.
    /// </summary>
    private async Task<string?> StartLookup(IPAddress address)
    {
        await _concurrencyGate.WaitAsync(_cts.Token).ConfigureAwait(false);
        try
        {
            // Double-check cache after acquiring semaphore
            if (_cache.TryGetValue(address, out var entry) && !entry.IsExpired)
            {
                return entry.Hostname;
            }

            string? hostname = null;
            var success = false;

            try
            {
                var hostEntry = await Dns.GetHostEntryAsync(address).ConfigureAwait(false);
                hostname = hostEntry.HostName;
                success = !string.IsNullOrWhiteSpace(hostname);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound ||
                                              ex.SocketErrorCode == SocketError.TryAgain)
            {
                // Negative cache for NXDOMAIN or temporary failures
                success = false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "DNS lookup failed for {Address}", address);
                success = false;
            }

            var cacheEntry = new DnsCacheEntry
            {
                Hostname = success ? hostname : null,
                Timestamp = DateTime.UtcNow,
                IsNegative = !success
            };

            // Evict oldest if cache is full
            if (_cache.Count >= _maxCacheSize)
            {
                var oldest = _cache.OrderBy(kvp => kvp.Value.Timestamp).FirstOrDefault();
                if (!oldest.Equals(default(KeyValuePair<IPAddress, DnsCacheEntry>)))
                {
                    _cache.TryRemove(oldest.Key, out _);
                }
            }

            _cache[address] = cacheEntry;
            return cacheEntry.Hostname;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            _concurrencyGate.Release();
            _inFlight.TryRemove(address, out _);
        }
    }

    /// <summary>
    /// Clears all cached entries (used when reverse DNS is disabled).
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        // In-flight lookups will complete but their results won't be cached
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _concurrencyGate.Dispose();
    }

    private sealed class DnsCacheEntry
    {
        public string? Hostname { get; init; }
        public DateTime Timestamp { get; init; }
        public bool IsNegative { get; init; }

        public bool IsExpired =>
            IsNegative
                ? DateTime.UtcNow - Timestamp > TimeSpan.FromMinutes(5) // Failure TTL
                : DateTime.UtcNow - Timestamp > TimeSpan.FromMinutes(30); // Success TTL
    }
}