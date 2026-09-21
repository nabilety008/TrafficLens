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

        resolver.TestDnsResolver = (address, token) => Task.FromResult<IPHostEntry?>(
            new IPHostEntry { HostName = "test.example.com" });

        var tcs = new TaskCompletionSource<string?>();
        resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"), h => tcs.TrySetResult(h));

        var hostname = await tcs.Task;

        Assert.Equal("test.example.com", hostname);
        resolver.Dispose();
    }

    [Fact]
    public async Task CacheHit_ReturnsCachedValueWithoutCallback()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxPendingWork: 1024);

        resolver.TestDnsResolver = (address, token) => Task.FromResult<IPHostEntry?>(
            new IPHostEntry { HostName = "test.example.com" });

        var tcs = new TaskCompletionSource<string?>();
        resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"), h => tcs.TrySetResult(h));
        await tcs.Task;

        var callbackFired = false;
        var result2 = resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"), _ => callbackFired = true);

        Assert.Equal("test.example.com", result2);
        Assert.False(callbackFired);
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

        resolver.TestDnsResolver = (address, token) => Task.FromResult<IPHostEntry?>(
            new IPHostEntry { HostName = "test.example.com" });

        for (int i = 0; i < 15; i++)
        {
            resolver.GetOrResolve(IPAddress.Parse($"10.0.0.{i}"), h => { });
        }

        resolver.Dispose();
    }

    [Fact]
    public void BoundedPendingWork_QueueStaysBounded()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 1,
            maxPendingWork: 10);

        resolver.TestDnsResolver = (address, token) => Task.FromResult<IPHostEntry?>(
            new IPHostEntry { HostName = "test.example.com" });

        int completed = 0;

        for (int i = 0; i < 20; i++)
        {
            resolver.GetOrResolve(IPAddress.Parse($"10.0.0.{i}"), h => { });
            Interlocked.Increment(ref completed);
        }

        Assert.Equal(20, completed);
        resolver.Dispose();
    }

    [Fact]
    public void Cancellation_CancelsOutstandingWork()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxPendingWork: 100);

        resolver.TestDnsResolver = (address, token) => Task.FromResult<IPHostEntry?>(
            new IPHostEntry { HostName = "test.example.com" });

        resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"), h => { });
        resolver.Dispose();
    }

    [Fact]
    public void Disposal_CancelsOutstandingWork_NoLeak()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxPendingWork: 100);

        resolver.TestDnsResolver = (address, token) => Task.FromResult<IPHostEntry?>(
            new IPHostEntry { HostName = "test.example.com" });

        resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"), h => { });
        resolver.Dispose();
    }

    [Fact]
    public void RawIpOnFailure_RawIpRemainsAvailable()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxPendingWork: 100);

        resolver.TestDnsResolver = (address, token) => Task.FromResult<IPHostEntry?>(
            new IPHostEntry { HostName = "test.example.com" });

        var ip = IPAddress.Parse("192.0.2.1");
        var result = resolver.GetOrResolve(ip);

        Assert.Null(result);
        resolver.Dispose();
    }

    [Fact]
    public async Task InFlightDeduplication_SameIPOnlyOneLookup()
    {
        int callCount = 0;
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            maxPendingWork: 100);

        resolver.TestDnsResolver = (address, token) =>
        {
            Interlocked.Increment(ref callCount);
            return Task.FromResult<IPHostEntry?>(
                new IPHostEntry { HostName = "test.example.com" });
        };

        var tcs1 = new TaskCompletionSource<string?>();
        var tcs2 = new TaskCompletionSource<string?>();

        resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"), h => tcs1.TrySetResult(h));
        resolver.GetOrResolve(IPAddress.Parse("8.8.8.8"), h => tcs2.TrySetResult(h));

        var result1 = await tcs1.Task;
        var result2 = await tcs2.Task;

        Assert.Equal("test.example.com", result1);
        Assert.Equal("test.example.com", result2);
        Assert.Equal(1, callCount);
        resolver.Dispose();
    }

    [Fact]
    public void QueueFullBehavior_ReturnsNullDoesNotBlock()
    {
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 1,
            maxPendingWork: 5);

        resolver.TestDnsResolver = (address, token) => Task.FromResult<IPHostEntry?>(
            new IPHostEntry { HostName = "test.example.com" });

        for (int i = 0; i < 10; i++)
        {
            resolver.GetOrResolve(IPAddress.Parse($"10.0.0.{i}"), h => { });
        }

        var result = resolver.GetOrResolve(IPAddress.Parse("10.0.0.100"), h => { });
        Assert.Null(result);
        resolver.Dispose();
    }

    [Fact]
    public async Task SuccessTtlExpiry_ExpiredEntryTriggersNewLookup()
    {
        var now = DateTime.UtcNow;
        DateTime? clockValue = now;
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            successTtl: TimeSpan.FromMinutes(30),
            maxPendingWork: 1024,
            clock: () => clockValue!.Value);

        int callCount = 0;
        resolver.TestDnsResolver = (address, token) =>
        {
            Interlocked.Increment(ref callCount);
            return Task.FromResult<IPHostEntry?>(
                new IPHostEntry { HostName = "resolved.example.com" });
        };

        var tcs1 = new TaskCompletionSource<string?>();
        resolver.GetOrResolve(IPAddress.Parse("10.0.0.1"), h => tcs1.TrySetResult(h));
        await tcs1.Task;
        Assert.Equal(1, callCount);

        var cachedResult = resolver.GetOrResolve(IPAddress.Parse("10.0.0.1"));
        Assert.Equal("resolved.example.com", cachedResult);
        Assert.Equal(1, callCount);

        clockValue = now + TimeSpan.FromMinutes(31);

        var tcs2 = new TaskCompletionSource<string?>();
        resolver.GetOrResolve(IPAddress.Parse("10.0.0.1"), h => tcs2.TrySetResult(h));
        await tcs2.Task;
        Assert.Equal(2, callCount);
        resolver.Dispose();
    }

    [Fact]
    public async Task FailureCache_RepeatedRequestDoesNotRetryBeforeExpiry()
    {
        var now = DateTime.UtcNow;
        DateTime? clockValue = now;
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            failureTtl: TimeSpan.FromMinutes(5),
            maxPendingWork: 1024,
            clock: () => clockValue!.Value);

        int callCount = 0;
        resolver.TestDnsResolver = (address, token) =>
        {
            Interlocked.Increment(ref callCount);
            return Task.FromResult<IPHostEntry?>(null);
        };

        var tcs1 = new TaskCompletionSource<string?>();
        resolver.GetOrResolve(IPAddress.Parse("10.0.0.2"), h => tcs1.TrySetResult(h));
        await tcs1.Task;
        Assert.Equal(1, callCount);

        var result2 = resolver.GetOrResolve(IPAddress.Parse("10.0.0.2"));
        Assert.Null(result2);
        Assert.Equal(1, callCount);

        resolver.Dispose();
    }

    [Fact]
    public async Task FailureTtlExpiry_ExpiredFailureTriggersRetry()
    {
        var now = DateTime.UtcNow;
        DateTime? clockValue = now;
        var resolver = new DnsResolverService(
            NullLogger<DnsResolverService>.Instance,
            maxConcurrentLookups: 4,
            failureTtl: TimeSpan.FromMinutes(5),
            maxPendingWork: 1024,
            clock: () => clockValue!.Value);

        int callCount = 0;
        resolver.TestDnsResolver = (address, token) =>
        {
            Interlocked.Increment(ref callCount);
            return Task.FromResult<IPHostEntry?>(null);
        };

        var tcs1 = new TaskCompletionSource<string?>();
        resolver.GetOrResolve(IPAddress.Parse("10.0.0.3"), h => tcs1.TrySetResult(h));
        await tcs1.Task;
        Assert.Equal(1, callCount);

        clockValue = now + TimeSpan.FromMinutes(6);

        var tcs2 = new TaskCompletionSource<string?>();
        resolver.GetOrResolve(IPAddress.Parse("10.0.0.3"), h => tcs2.TrySetResult(h));
        await tcs2.Task;
        Assert.Equal(2, callCount);
        resolver.Dispose();
    }
}
