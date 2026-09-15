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
}
