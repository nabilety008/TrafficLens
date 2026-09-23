using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace TrafficLens.Infrastructure.Services;

/// <summary>
/// Bounded reverse DNS resolver with TTL cache, bounded pending queue,
/// concurrency limiting, failure caching, and cooperative cancellation.
/// Designed for UI-friendly background resolution of remote IP addresses
/// in the Connections page. Both active lookups and pending work are bounded.
/// </summary>
public sealed class DnsResolverService : IDisposable
{
    private readonly ILogger<DnsResolverService> _logger;
    private readonly TimeSpan _successTtl;
    private readonly TimeSpan _failureTtl;
    private readonly int _maxCacheSize;
    private readonly int _maxPendingWork;
    private readonly Func<DateTime> _clock;
    private readonly ConcurrentDictionary<IPAddress, DnsCacheEntry> _cache = new();
    private readonly ConcurrentDictionary<IPAddress, TaskCompletionSource<string?>> _inFlight = new();
    private readonly Channel<DnsWorkItem> _workQueue;
    private readonly Task[] _workers;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>
    /// Optional test override for DNS resolution. When set, this function is used instead of Dns.GetHostEntryAsync.
    /// </summary>
    internal Func<IPAddress, CancellationToken, Task<IPHostEntry?>>? TestDnsResolver { get; set; }

    /// <summary>
    /// Work item for the DNS resolution queue.
    /// </summary>
    private sealed class DnsWorkItem
    {
        public required IPAddress Address { get; init; }
        public required TaskCompletionSource<string?> Completion { get; init; }
    }

    public DnsResolverService(
        ILogger<DnsResolverService> logger,
        int maxConcurrentLookups = 4,
        TimeSpan? successTtl = null,
        TimeSpan? failureTtl = null,
        int maxCacheSize = 1024,
        int maxPendingWork = 1024,
        Func<DateTime>? clock = null)
    {
        if (maxConcurrentLookups <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentLookups), "Must be positive");
        }
        if (maxCacheSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCacheSize), "Must be positive");
        }
        if (maxPendingWork <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPendingWork), "Must be positive");
        }

        _logger = logger;
        _successTtl = successTtl ?? TimeSpan.FromMinutes(30);
        _failureTtl = failureTtl ?? TimeSpan.FromMinutes(5);
        _maxCacheSize = maxCacheSize;
        _maxPendingWork = maxPendingWork;
        _clock = clock ?? (() => DateTime.UtcNow);

        // Bounded channel for pending DNS work
        var options = new BoundedChannelOptions(maxPendingWork)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };
        _workQueue = Channel.CreateBounded<DnsWorkItem>(options);

        // Start fixed worker pool
        _workers = new Task[maxConcurrentLookups];
        for (int i = 0; i < maxConcurrentLookups; i++)
        {
            _workers[i] = Task.Run(ProcessQueueAsync);
        }
    }

    /// <summary>
    /// Gets the cached hostname for an IP, or queues a background lookup if not cached.
    /// Returns the cached value immediately if available (including negative cache),
    /// otherwise returns null and queues a background lookup that will complete
    /// the provided TaskCompletionSource when done.
    /// Returns null if the resolver is disposed, the address is ignored, or the
    /// pending queue is full (backpressure).
    /// </summary>
    public string? GetOrResolve(IPAddress address, Action<string?>? onResolved = null)
    {
        if (_disposed || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.Loopback) || address.Equals(IPAddress.IPv6Loopback))
        {
            return null;
        }

        // Check cache first
        if (_cache.TryGetValue(address, out var cachedEntry) && !cachedEntry.IsExpired(_clock()))
        {
            return cachedEntry.Hostname;
        }

        // Check if there's already an in-flight lookup for this address
        bool newlyCreated = false;
        var existingTcs = _inFlight.GetOrAdd(address, _ =>
        {
            newlyCreated = true;
            return new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        });
        
        // If we created a new TCS, queue the work
        if (newlyCreated)
        {
            var workItem = new DnsWorkItem { Address = address, Completion = existingTcs };
            
            if (!_workQueue.Writer.TryWrite(new DnsWorkItem { Address = address, Completion = existingTcs }))
            {
                // Queue is full - backpressure: reject this request
                _inFlight.TryRemove(address, out _);
                return null;
            }
        }

        // Check if the address is still in _inFlight (might have been removed due to queue full)
        if (!_inFlight.TryGetValue(address, out var tcs))
        {
            return null;
        }

        // Attach continuation to invoke callback when complete
        tcs.Task.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && onResolved is not null)
            {
                onResolved(t.Result);
            }
        }, TaskContinuationOptions.OnlyOnRanToCompletion);

        // Return cached value if available (may be null if not yet resolved)
        _cache.TryGetValue(address, out var entry);
        return entry?.IsExpired(_clock()) == false ? entry.Hostname : null;
    }

    /// <summary>
    /// Processes work items from the queue.
    /// </summary>
    private async Task ProcessQueueAsync()
    {
        var reader = _workQueue.Reader;
        await foreach (var workItem in reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
        {
            if (_disposed || _cts.Token.IsCancellationRequested)
            {
                workItem.Completion.TrySetCanceled();
                continue;
            }

            var address = workItem.Address;

            try
            {
                // Double-check cache (another worker might have resolved it)
                if (_cache.TryGetValue(address, out var entry) && !entry.IsExpired(_clock()))
                {
                    workItem.Completion.TrySetResult(entry.Hostname);
                    continue;
                }

                string? hostname = null;
                var success = false;

                try
                {
                    var hostEntry = TestDnsResolver != null
                        ? await TestDnsResolver(address, _cts.Token).ConfigureAwait(false)
                        : await Dns.GetHostEntryAsync(address).ConfigureAwait(false);
                    hostname = hostEntry?.HostName;
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
                    ExpiresAt = _clock() + (success ? _successTtl : _failureTtl),
                    IsNegative = !success
                };

                // Evict oldest if cache is full (LRU by earliest expiry)
                if (_cache.Count >= _maxCacheSize)
                {
                    var oldest = _cache.OrderBy(kvp => kvp.Value.ExpiresAt).FirstOrDefault();
                    if (!oldest.Equals(default(KeyValuePair<IPAddress, DnsCacheEntry>)))
                    {
                        _cache.TryRemove(oldest.Key, out _);
                    }
                }

                _cache[address] = cacheEntry;
                _inFlight.TryRemove(address, out _);
                workItem.Completion.TrySetResult(cacheEntry.Hostname);
            }
            catch (OperationCanceledException)
            {
                workItem.Completion.TrySetCanceled();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in DNS worker for {Address}", workItem.Address);
                workItem.Completion.TrySetException(ex);
            }
        }
    }

    /// <summary>
    /// Clears all cached entries (used when reverse DNS is disabled).
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();

        // Wait for workers to complete (with timeout)
        try
        {
            Task.WaitAll(_workers, TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Ignore exceptions during shutdown
        }

        _cts.Dispose();

        // Complete the channel to release any waiting writers
        _workQueue.Writer.Complete();
    }

    private sealed class DnsCacheEntry
    {
        public string? Hostname { get; init; }
        public DateTime ExpiresAt { get; init; }
        public bool IsNegative { get; init; }

        public bool IsExpired(DateTime now) => now >= ExpiresAt;
    }
}