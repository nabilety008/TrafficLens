using Microsoft.Extensions.Logging.Abstractions;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.History;
using TrafficLens.Core.Models;

namespace TrafficLens.Infrastructure.Tests;

public class TrafficHistoryServiceTests
{
    private static DateTime Utc(int y = 2026, int m = 6, int d = 15, int h = 12, int min = 0) =>
        new(y, m, d, h, min, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FeedsDeltas_Flushes_FlushOnShutdown()
    {
        var now = Utc();
        var clock = now;
        Func<DateTime> utcNow = () => clock;

        var dbPath = Path.Combine(Path.GetTempPath(), $"tl_svc_test_{Guid.NewGuid():N}.db");
        try
        {
            var collector = new FakeCollector();
            var adapterProvider = new FakeAdapterProvider();
            using var repo = new SqliteTrafficHistoryRepository(dbPath, NullLogger<SqliteTrafficHistoryRepository>.Instance);

            var svc = new TrafficHistoryService(
                collector, adapterProvider, repo,
                NullLogger<TrafficHistoryService>.Instance,
                timeZone: TimeZoneInfo.Local,
                flushInterval: TimeSpan.FromSeconds(1),
                utcNow: utcNow);

            adapterProvider.SetAdapters(new[]
            {
                new NetworkAdapterInfo("eth0", "Ethernet", "Desc", "00:00:00:00:00:00", NetworkAdapterKind.Ethernet, true, true)
            });

            await svc.StartAsync(CancellationToken.None);

            collector.RaiseCounter("eth0", 1000, 500, now);
            clock = now.AddSeconds(2);
            collector.RaiseCounter("eth0", 1200, 600, clock);

            await Task.Delay(2000);
            clock = now.AddSeconds(61);
            await svc.StopAsync();

            var snapshot = svc.GetSnapshot();
            Assert.True(snapshot.IsAvailable);

            var lifetime = await repo.QueryLifetimeAsync(CancellationToken.None);
            Assert.Equal(200, lifetime.DownloadBytes);
            Assert.Equal(100, lifetime.UploadBytes);
        }
        finally
        {
            foreach (var suffix in new[] { ".db", "-wal", "-shm" })
            {
                try
                {
                    if (File.Exists(dbPath + suffix))
                    {
                        File.Delete(dbPath + suffix);
                    }
                }
                catch (IOException)
                {
                }
            }
        }
    }

    [Fact]
    public async Task Restart_NoDuplicateUsage()
    {
        var now = Utc();
        var clock = now;
        Func<DateTime> utcNow = () => clock;
        var dbPath = Path.Combine(Path.GetTempPath(), $"tl_svc_restart_{Guid.NewGuid():N}.db");

        try
        {
            var collector = new FakeCollector();
            var adapterProvider = new FakeAdapterProvider();
            adapterProvider.SetAdapters(new[]
            {
                new NetworkAdapterInfo("eth0", "Ethernet", "Desc", "00:00:00:00:00:00", NetworkAdapterKind.Ethernet, true, true)
            });

            using var repo = new SqliteTrafficHistoryRepository(dbPath, NullLogger<SqliteTrafficHistoryRepository>.Instance);
            var svc1 = new TrafficHistoryService(collector, adapterProvider, repo,
                NullLogger<TrafficHistoryService>.Instance, utcNow: utcNow);
await svc1.StartAsync(CancellationToken.None);
            collector.RaiseCounter("eth0", 1000, 500, now);
            collector.RaiseCounter("eth0", 1100, 520, now.AddSeconds(2));
            clock = now.AddSeconds(61);
            await svc1.StopAsync();
            svc1.Dispose();

            var collector2 = new FakeCollector();
            var adapterProvider2 = new FakeAdapterProvider();
            adapterProvider2.SetAdapters(new[]
            {
                new NetworkAdapterInfo("eth0", "Ethernet", "Desc", "00:00:00:00:00:00", NetworkAdapterKind.Ethernet, true, true)
            });

            var svc2 = new TrafficHistoryService(collector2, adapterProvider2, repo,
                NullLogger<TrafficHistoryService>.Instance, utcNow: utcNow);
            await svc2.StartAsync(CancellationToken.None);
            collector2.RaiseCounter("eth0", 1200, 600, clock);
            collector2.RaiseCounter("eth0", 1300, 620, clock.AddSeconds(5));
            clock = now.AddSeconds(121);
            await svc2.StopAsync();
            svc2.Dispose();

            var lifetime = await repo.QueryLifetimeAsync(CancellationToken.None);
            Assert.Equal(200, lifetime.DownloadBytes);
            Assert.Equal(40, lifetime.UploadBytes);
        }
        finally
        {
            foreach (var suffix in new[] { ".db", "-wal", "-shm" })
            {
                try
                {
                    if (File.Exists(dbPath + suffix))
                    {
                        File.Delete(dbPath + suffix);
                    }
                }
                catch (IOException)
                {
                }
            }
        }
    }

    [Fact]
    public async Task TunnelExcluded_ByDefault()
    {
        var now = Utc();
        var clock = now;
        Func<DateTime> utcNow = () => clock;
        var dbPath = Path.Combine(Path.GetTempPath(), $"tl_svc_tunnel_{Guid.NewGuid():N}.db");

        try
        {
            var collector = new FakeCollector();
            var adapterProvider = new FakeAdapterProvider();
            adapterProvider.SetAdapters(new[]
            {
                new NetworkAdapterInfo("eth0", "Ethernet", "Desc", "00:00:00:00:00:00", NetworkAdapterKind.Ethernet, true, true),
                new NetworkAdapterInfo("tun0", "Tunnel", "Desc", "00:00:00:00:00:01", NetworkAdapterKind.Tunnel, true, false)
            });

            using var repo = new SqliteTrafficHistoryRepository(dbPath, NullLogger<SqliteTrafficHistoryRepository>.Instance);
            var svc = new TrafficHistoryService(collector, adapterProvider, repo,
                NullLogger<TrafficHistoryService>.Instance, utcNow: utcNow);
            await svc.StartAsync(CancellationToken.None);
            collector.RaiseCounter("eth0", 1000, 500, now);
            collector.RaiseCounter("tun0", 50000, 20000, now);
            clock = now.AddSeconds(2);
            collector.RaiseCounter("eth0", 1200, 600, clock);
            clock = now.AddSeconds(61);
            await svc.StopAsync();

            var lifetime = await repo.QueryLifetimeAsync(CancellationToken.None);
            Assert.Equal(200, lifetime.DownloadBytes);
            Assert.Equal(100, lifetime.UploadBytes);
        }
        finally
        {
            foreach (var suffix in new[] { ".db", "-wal", "-shm" })
            {
                try
                {
                    if (File.Exists(dbPath + suffix))
                    {
                        File.Delete(dbPath + suffix);
                    }
                }
                catch (IOException)
                {
                }
            }
        }
    }

    #pragma warning disable CS0067
    private sealed class FakeCollector : INetworkTrafficCollector
    {
        public event EventHandler<NetworkCounterSample>? CounterSampleReady;
        public event EventHandler<NetworkSpeedSample>? SpeedSampleReady;
        public event EventHandler? NetworkChanged;
        public IReadOnlyList<NetworkCounterSample> GetCurrentCounterSamples() => Array.Empty<NetworkCounterSample>();
        public IReadOnlyList<NetworkSpeedSample> GetCurrentSamples() => Array.Empty<NetworkSpeedSample>();
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
        public void RaiseCounter(string id, long received, long sent, DateTime timestamp) =>
            CounterSampleReady?.Invoke(this, new NetworkCounterSample(id, id, received, sent, timestamp));
    }
#pragma warning restore CS0067

    private sealed class FakeAdapterProvider : INetworkAdapterProvider
    {
        private IReadOnlyList<NetworkAdapterInfo> _adapters = Array.Empty<NetworkAdapterInfo>();
        public event EventHandler? AdaptersChanged;
        public IReadOnlyList<NetworkAdapterInfo> GetAdapters() => _adapters;
        public NetworkAdapterInfo? GetDefaultAdapter() => _adapters.FirstOrDefault(a => a.IsUp);
        public void SetAdapters(IReadOnlyList<NetworkAdapterInfo> adapters)
        {
            _adapters = adapters;
            AdaptersChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
