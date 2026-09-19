using Microsoft.Extensions.Logging.Abstractions;
using TrafficLens.Core.History;

namespace TrafficLens.Infrastructure.Tests;

public class SqliteTrafficHistoryRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tl_test_{Guid.NewGuid():N}.db");
    private readonly SqliteTrafficHistoryRepository _repo;
    private static readonly TimeZoneInfo TestZone = TimeZoneInfo.Local;

    public SqliteTrafficHistoryRepositoryTests()
    {
        _repo = new SqliteTrafficHistoryRepository(_dbPath, NullLogger<SqliteTrafficHistoryRepository>.Instance);
    }

    public void Dispose()
    {
        _repo.Dispose();
        foreach (var suffix in new[] { ".db", "-wal", "-shm" })
        {
            TryDelete(_dbPath + suffix);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public async Task InitializeAsync_CreatesSchema()
    {
        await _repo.InitializeAsync(CancellationToken.None);
        Assert.True(_repo.IsAvailable);
        Assert.Null(_repo.LastError);
    }

    [Fact]
    public async Task AppendBucketsAsync_IdempotentPerBucketStart()
    {
        await _repo.InitializeAsync(CancellationToken.None);
        var bucket = new TrafficHistoryBucket(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            60, 1000, 500);
        var first = await _repo.AppendBucketsAsync(new[] { bucket }, TestZone, CancellationToken.None);
        var second = await _repo.AppendBucketsAsync(new[] { bucket }, TestZone, CancellationToken.None);
        Assert.Equal(1, first);
        Assert.Equal(0, second);
    }

    [Fact]
    public async Task QueryDailyAsync_ReturnsCorrectRange()
    {
        await _repo.InitializeAsync(CancellationToken.None);
        var b1 = new TrafficHistoryBucket(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), 60, 100, 0);
        var b2 = new TrafficHistoryBucket(new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc), 60, 0, 200);
        var b3 = new TrafficHistoryBucket(new DateTime(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc), 60, 50, 50);
        await _repo.AppendBucketsAsync(new[] { b1, b2, b3 }, TestZone, CancellationToken.None);

        var daily = await _repo.QueryDailyAsync(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 3),
            CancellationToken.None);

        Assert.Equal(2, daily.Count);
        Assert.Equal(100, daily[new DateOnly(2026, 1, 1)].DownloadBytes);
        Assert.Equal(0, daily[new DateOnly(2026, 1, 1)].UploadBytes);
        Assert.Equal(200, daily[new DateOnly(2026, 1, 2)].UploadBytes);
    }

    [Fact]
    public async Task QueryLifetimeAsync_SumsAllDays()
    {
        await _repo.InitializeAsync(CancellationToken.None);
        var b1 = new TrafficHistoryBucket(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), 60, 100, 0);
        var b2 = new TrafficHistoryBucket(new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc), 60, 0, 200);
        await _repo.AppendBucketsAsync(new[] { b1, b2 }, TestZone, CancellationToken.None);

        var lifetime = await _repo.QueryLifetimeAsync(CancellationToken.None);
        Assert.Equal(100, lifetime.DownloadBytes);
        Assert.Equal(200, lifetime.UploadBytes);
    }

    [Fact]
    public async Task PruneRawSamplesBeforeAsync_DeletesOldBuckets_KeepsDaily()
    {
        await _repo.InitializeAsync(CancellationToken.None);
        var old = new TrafficHistoryBucket(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), 60, 10, 20);
        var recent = new TrafficHistoryBucket(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 60, 100, 200);
        await _repo.AppendBucketsAsync(new[] { old, recent }, TestZone, CancellationToken.None);

        var deleted = await _repo.PruneRawSamplesBeforeAsync(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        Assert.Equal(1, deleted);

        var lifetime = await _repo.QueryLifetimeAsync(CancellationToken.None);
        Assert.Equal(110, lifetime.DownloadBytes);
        Assert.Equal(220, lifetime.UploadBytes);
    }

    [Fact]
    public async Task ReopenDatabase_PreservesSchemaVersion()
    {
        await _repo.InitializeAsync(CancellationToken.None);
        await _repo.AppendBucketsAsync(new[]
        {
            new TrafficHistoryBucket(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 60, 50, 50)
        }, TestZone, CancellationToken.None);
        _repo.Dispose();

        using var repo2 = new SqliteTrafficHistoryRepository(_dbPath, NullLogger<SqliteTrafficHistoryRepository>.Instance);
        await repo2.InitializeAsync(CancellationToken.None);
        Assert.True(repo2.IsAvailable);
        var lifetime = await repo2.QueryLifetimeAsync(CancellationToken.None);
        Assert.Equal(50, lifetime.DownloadBytes);
    }

    [Fact]
    public async Task InitializeAsync_MigratesV1History_BackFillsLifetimeTotal()
    {
        // Create a throwaway DB holding a genuine schema-v1 layout exactly as
        // v0.1.1 (released) would have left it: traffic_samples + daily_usage,
        // PRAGMA user_version = 1, and NO lifetime_totals table.
        var v1Path = Path.Combine(Path.GetTempPath(), $"tl_migrate_{Guid.NewGuid():N}.db");
        try
        {
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={v1Path}"))
            {
                conn.Open();
                using var tx = conn.BeginTransaction();
                foreach (var sql in new[]
                {
                    "CREATE TABLE traffic_samples (" +
                    "bucket_start_utc INTEGER PRIMARY KEY, " +
                    "bucket_duration_seconds INTEGER NOT NULL, " +
                    "download_bytes INTEGER NOT NULL, " +
                    "upload_bytes INTEGER NOT NULL);",
                    "CREATE TABLE daily_usage (" +
                    "local_date TEXT PRIMARY KEY, " +
                    "download_bytes INTEGER NOT NULL, " +
                    "upload_bytes INTEGER NOT NULL);",
                    "INSERT INTO daily_usage (local_date, download_bytes, upload_bytes) " +
                    "VALUES ('2026-01-01', 100, 0);",
                    "INSERT INTO daily_usage (local_date, download_bytes, upload_bytes) " +
                    "VALUES ('2026-01-02', 0, 200);",
                    "PRAGMA user_version = 1;"
                })
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }

            using var repo2 = new SqliteTrafficHistoryRepository(v1Path, NullLogger<SqliteTrafficHistoryRepository>.Instance);
            await repo2.InitializeAsync(CancellationToken.None);
            Assert.True(repo2.IsAvailable);

            // daily_usage still contains the pre-upgrade rows.
            var daily = await repo2.QueryDailyAsync(
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 1, 3),
                CancellationToken.None);
            Assert.Equal(100, daily[new DateOnly(2026, 1, 1)].DownloadBytes);
            Assert.Equal(200, daily[new DateOnly(2026, 1, 2)].UploadBytes);

            // Lifetime total was back-filled from daily_usage, not lost.
            var lifetime = await repo2.QueryLifetimeAsync(CancellationToken.None);
            Assert.Equal(100, lifetime.DownloadBytes);
            Assert.Equal(200, lifetime.UploadBytes);
        }
        finally
        {
            foreach (var suffix in new[] { ".db", "-wal", "-shm" })
            {
                TryDelete(v1Path + suffix);
            }
        }
    }
}
