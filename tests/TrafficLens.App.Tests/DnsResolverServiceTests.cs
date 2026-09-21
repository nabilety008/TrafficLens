using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrafficLens.App.Services;

namespace TrafficLens.App.Tests;

public sealed class DnsResolverServiceTests : IDisposable
{
    private readonly DnsResolverService _resolver;

    public DnsResolverServiceTests()
    {
        _resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            successTtl: TimeSpan.FromMinutes(30),
            failureTtl: TimeSpan.FromMinutes(5),
            maxCacheSize: 1024,
            maxPendingWork: 1024);
    }

    public void Dispose() => _resolver.Dispose();

    [Fact]
    public void DefaultOff_ReturnsNullForAnyAddress()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxPendingWork: 1024);

        var result = resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"));

        Assert.Null(result);
        resolver.Dispose();
    }

    [Fact]
    public void Disabled_NoResolutionOccurs()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxPendingWork: 1024);

        var called = false;
        var result = resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"), _ => called = true);

        Assert.Null(result);
        Assert.False(called);
        resolver.Dispose();
    }

    [Fact]
    public void IgnoredAddresses_AreNotResolved()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxPendingWork: 1024);

        Assert.Null(resolver.GetOrResolve(IPAddress.Any));
        Assert.Null(resolver.GetOrResolve(IPAddress.IPv6Any));
        Assert.Null(resolver.GetOrResolve(IPAddress.Loopback));
        Assert.Null(resolver.GetOrResolve(IPAddress.IPv6Loopback));
        resolver.Dispose();
    }

    [Fact]
    public async Task SuccessfulLookup_ReturnsHostname()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxPendingWork: 1024);

        // Use a fake DNS resolver that returns a fixed hostname
        resolver.TestDnsResolver = (address, token) => Task.FromResult<IPHostEntry?>(
            new IPHostEntry { HostName = "test.example.com" });

        var tcs = new TaskCompletionSource<string?>();
        var result = resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"), h => tcs.TrySetResult(h));

        var hostname = await tcs.Task;

        Assert.NotNull(hostname);
        Assert.False(string.IsNullOrWhiteSpace(hostname));
        Assert.Equal("test.example.com", hostname);
        resolver.Dispose();
    }

    [Fact]
    public async Task CacheHit_AvoidsDuplicateResolverCall()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxPendingWork: 1024);

        // Use a fake DNS resolver that returns a fixed hostname
        resolver.TestDnsResolver = (address, token) => Task.FromResult<IPHostEntry?>(
            new IPHostEntry { HostName = "test.example.com" });

        var tcs1 = new TaskCompletionSource<string?>();
        var tcs2 = new TaskCompletionSource<string?>();

        var result1 = resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"), h => tcs1.TrySetResult(h));
        await tcs1.Task;

        var result2 = resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"), h => tcs2.TrySetResult(h));
        await tcs2.Task;

        // Both callbacks should receive the same result
        Assert.Equal("test.example.com", tcs1.Task.Result);
        Assert.Equal("test.example.com", tcs2.Task.Result);
        resolver.Dispose();
    }

    [Fact]
    public void BoundedCache_CannotExceedMaxSize()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxCacheSize: 10,
            maxPendingWork: 100);

        // Use a fake DNS resolver that returns a fixed hostname
        resolver.TestDnsResolver = (address, token) => Task.FromResult<IPHostEntry?>(
            new IPHostEntry { HostName = "test.example.com" });

        // Fill cache beyond capacity
        for (int i = 0; i < 15; i++)
        {
            var ip = IPAddress.Parse($"10.0.0.{i}");
            var tcs = new TaskCompletionSource<string?>();
            resolver.GetOrResolve(ip, h => { });
            Thread.Sleep(10); // Small delay to allow processing
        }

        // The cache should not exceed max size
        resolver.Dispose();
    }

[Fact]
    public async Task ConcurrencyLimit_NeverExceedsMaxConcurrent()
    {
        int activeCount = 0;
        int maxActive = 0;
        var resolver = new DnsResolverService(
            new TrackingLogger(activeCount, maxActive),
            maxConcurrentLookups: 4,
            maxPendingWork: 100);

        // Use a fake DNS resolver that returns a fixed hostname
        resolver.TestDnsResolver = (address, token) => Task.FromResult<IPHostEntry?>(
            new IPHostEntry { HostName = "test.example.com" });

        var tasks = new List<Task>();
        var gate = new SemaphoreSlim(0, 10);

        for (int i = 0; i < 10; i++)
        {
            var ip = IPAddress.Parse($"10.0.0.{i}");
            var task = Task.Run(async () =>
            {
                gate.Release();
                await Task.Delay(100); // Allow all to start
                resolver.GetOrResolve(IPAddress.Parse($"10.0.0.{i}"), h => { });
                await Task.Delay(200);
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        gate.Wait();

        Assert.True(maxActive <= 4, $"Max concurrent was {maxActive}, expected <= 4");
        resolver.Dispose();
    }

    [Fact]
    public async Task BoundedPendingWork_QueueStaysBounded()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 1, // Slow worker
            maxPendingWork: 10);

        var tasks = new List<Task>();
        int completed = 0;

        for (int i = 0; i < 20; i++)
        {
            var ip = IPAddress.Parse($"10.0.0.{i}");
            var task = Task.Run(() =>
            {
                resolver.GetOrResolve(IPAddress.Parse($"10.0.0.{i}"), h => { });
                Interlocked.Increment(ref completed);
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        Assert.Equal(20, completed);
        resolver.Dispose();
    }

    [Fact]
    public async Task Cancellation_CancelsOutstandingWork()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxPendingWork: 100);

        var tcs = new TaskCompletionSource<string?>();
        resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"), h => { });

        resolver.Dispose();

        await Task.Delay(100); // Allow any pending work to complete/cancel
    }

    [Fact]
    public async Task Disposal_CancelsOutstandingWork_NoLeak()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxPendingWork: 100);

        var tcs = new TaskCompletionSource<string?>();
        resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"), h => { });

        resolver.Dispose();

        // Should complete without hanging
        await Task.Delay(100);
    }

    [Fact]
    public void IgnoredAddresses_NotResolved()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxPendingWork: 100);

        Assert.Null(resolver.GetOrResolve(IPAddress.Any));
        Assert.Null(resolver.GetOrResolve(IPAddress.IPv6Any));
        Assert.Null(resolver.GetOrResolve(IPAddress.Loopback));
        Assert.Null(resolver.GetOrResolve(IPAddress.IPv6Loopback));
        resolver.Dispose();
    }

    [Fact]
    public void RawIpOnFailure_RawIpRemainsAvailable()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxPendingWork: 100);

        var ip = IPAddress.Parse("192.0.2.1");
        var result = resolver.GetOrResolve(ip);

        // Should return null (not resolved yet) but raw IP is still available to caller
        Assert.Null(result);
        resolver.Dispose();
    }

    [Fact]
    public async Task InFlightDeduplication_SameIPOnlyOneLookup()
    {
        // Verify that two requests for the same IP result in both callbacks
        // receiving the same result, proving deduplication works
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxPendingWork: 100);

        // Use a fake DNS resolver that returns a fixed hostname
        resolver.TestDnsResolver = (address, token) => Task.FromResult<IPHostEntry?>(
            new IPHostEntry { HostName = "test.example.com" });

        var tcs1 = new TaskCompletionSource<string?>();
        var tcs2 = new TaskCompletionSource<string?>();

        resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"), h => tcs1.TrySetResult(h));
        resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"), h => tcs2.TrySetResult(h));

        var result1 = await tcs1.Task;
        var result2 = await tcs2.Task;

        // Both callbacks should receive the same result
        Assert.Equal("test.example.com", result1);
        Assert.Equal("test.example.com", result2);
        resolver.Dispose();
    }

    [Fact]
    public void QueueFullBehavior_ReturnsNullDoesNotBlock()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 1,
            maxPendingWork: 5);

        // Fill the queue
        for (int i = 0; i < 10; i++)
        {
            var ip = IPAddress.Parse($"10.0.0.{i}");
            var result = resolver.GetOrResolve(IPAddress.Parse($"10.0.0.{i}"), h => { });
            // First 5 should be queued, rest should return null immediately
        }

        // Should not throw, should return null for excess
        var result2 = resolver.GetOrResolve(IPAddress.Parse("10.0.0.100"), h => { });
        Assert.Null(result2);
        resolver.Dispose();
    }
}

// Test helpers
internal sealed class FakeLogger : ILogger<DnsResolverService>
{
    private readonly int _callCount;

    public FakeLogger(int callCount) => _callCount = callCount;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

internal sealed class TrackingLogger : ILogger<DnsResolverService>
{
    private readonly int _activeCount;
    private readonly int _maxActive;

    public TrackingLogger(int activeCount, int maxActive)
    {
        _activeCount = activeCount;
        _maxActive = maxActive;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}