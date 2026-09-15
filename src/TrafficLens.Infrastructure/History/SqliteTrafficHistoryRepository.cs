using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TrafficLens.Core.History;

namespace TrafficLens.Infrastructure.History;

/// <summary>
/// SQLite-backed <see cref="ITrafficHistoryRepository"/>.
///
/// Storage model (see docs/DATABASE.md):
/// <list type="bullet">
/// <item><c>traffic_samples</c> — append-only minute buckets, keyed by UTC unix
/// seconds. Source of truth; safe to compact because...</item>
/// <item><c>daily_usage</c> — per-LOCAL-day rollup maintained in the same
/// transaction, so Today/7d/30d/Lifetime never scan raw buckets. Kept forever.</item>
/// <item><c>PRAGMA user_version</c> — explicit schema version for migrations.</item>
/// </list>
///
/// Writes are serialized with a semaphore and each append runs in one
/// transaction using <c>INSERT OR IGNORE</c> plus a <c>changes()</c> guard, so
/// re-feeding a bucket (e.g. after a restart) can never double-count. All
/// operations are executed synchronously on the caller's thread; callers are
/// responsible for invoking them off the UI thread (the history service does).
/// Failures are recorded in <see cref="LastError"/> and never thrown at callers
/// (initialization failures disable the repository instead).
/// </summary>
public sealed class SqliteTrafficHistoryRepository : ITrafficHistoryRepository
{
    public const int SchemaVersion = 1;

    private const string DateFormat = "yyyy-MM-dd";

    private readonly string _connectionString;
    private readonly ILogger<SqliteTrafficHistoryRepository> _logger;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private bool _disposed;

    public SqliteTrafficHistoryRepository(
        string databasePath,
        ILogger<SqliteTrafficHistoryRepository> logger)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
        _logger = logger;
    }

    public bool IsAvailable { get; private set; }

    public string? LastError { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            Migrate();
            IsAvailable = true;
            LastError = null;
            _logger.LogInformation("Traffic history database ready (schema v{SchemaVersion})", SchemaVersion);
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            LastError = $"History database is unavailable: {ex.Message}";
            _logger.LogError(ex, "Failed to initialize traffic history database; history will be unavailable");
        }

        return Task.CompletedTask;
    }

    public Task<int> AppendBucketsAsync(
        IReadOnlyList<TrafficHistoryBucket> buckets,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        if (buckets.Count == 0)
        {
            return Task.FromResult(0);
        }

        _writeGate.Wait(cancellationToken);
        try
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();

            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                "INSERT OR IGNORE INTO traffic_samples " +
                "(bucket_start_utc, bucket_duration_seconds, download_bytes, upload_bytes) " +
                "VALUES ($start, $duration, $download, $upload);";
            var startParameter = insert.Parameters.Add("$start", SqliteType.Integer);
            var durationParameter = insert.Parameters.Add("$duration", SqliteType.Integer);
            var downloadParameter = insert.Parameters.Add("$download", SqliteType.Integer);
            var uploadParameter = insert.Parameters.Add("$upload", SqliteType.Integer);

            using var changed = connection.CreateCommand();
            changed.Transaction = transaction;
            changed.CommandText = "SELECT changes();";

            using var upsertDaily = connection.CreateCommand();
            upsertDaily.Transaction = transaction;
            upsertDaily.CommandText =
                "INSERT INTO daily_usage (local_date, download_bytes, upload_bytes) " +
                "VALUES ($date, $download, $upload) " +
                "ON CONFLICT(local_date) DO UPDATE SET " +
                "download_bytes = download_bytes + excluded.download_bytes, " +
                "upload_bytes = upload_bytes + excluded.upload_bytes;";
            var dateParameter = upsertDaily.Parameters.Add("$date", SqliteType.Text);
            var dailyDownload = upsertDaily.Parameters.Add("$download", SqliteType.Integer);
            var dailyUpload = upsertDaily.Parameters.Add("$upload", SqliteType.Integer);

            var stored = 0;
            foreach (var bucket in buckets)
            {
                startParameter.Value = ToUnixSeconds(bucket.BucketStartUtc);
                durationParameter.Value = bucket.DurationSeconds;
                downloadParameter.Value = bucket.DownloadBytes;
                uploadParameter.Value = bucket.UploadBytes;
                insert.ExecuteNonQuery();

                var inserted = Convert.ToInt64(changed.ExecuteScalar(), CultureInfo.InvariantCulture);
                if (inserted == 0)
                {
                    continue;
                }

                dateParameter.Value = HistoryRangeCalculator
                    .LocalDateOf(bucket.BucketStartUtc, timeZone)
                    .ToString(DateFormat, CultureInfo.InvariantCulture);
                dailyDownload.Value = bucket.DownloadBytes;
                dailyUpload.Value = bucket.UploadBytes;
                upsertDaily.ExecuteNonQuery();
                stored++;
            }

            transaction.Commit();
            LastError = null;
            return Task.FromResult(stored);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastError = $"Failed to persist history: {ex.Message}";
            _logger.LogError(ex, "Failed to append {Count} history bucket(s)", buckets.Count);
            return Task.FromResult(0);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public Task<IReadOnlyDictionary<DateOnly, TrafficUsage>> QueryDailyAsync(
        DateOnly startInclusive,
        DateOnly endExclusive,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<DateOnly, TrafficUsage>();
        try
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT local_date, download_bytes, upload_bytes FROM daily_usage " +
                "WHERE local_date >= $start AND local_date < $end;";
            command.Parameters.AddWithValue("$start", startInclusive.ToString(DateFormat, CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$end", endExclusive.ToString(DateFormat, CultureInfo.InvariantCulture));

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!DateOnly.TryParseExact(reader.GetString(0), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    continue;
                }

                result[date] = new TrafficUsage(reader.GetInt64(1), reader.GetInt64(2));
            }

            LastError = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastError = $"Failed to read history: {ex.Message}";
            _logger.LogError(ex, "Failed to query daily history");
        }

        return Task.FromResult<IReadOnlyDictionary<DateOnly, TrafficUsage>>(result);
    }

    public Task<TrafficUsage> QueryLifetimeAsync(CancellationToken cancellationToken)
    {
        var usage = TrafficUsage.Empty;
        try
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COALESCE(SUM(download_bytes), 0), COALESCE(SUM(upload_bytes), 0) FROM daily_usage;";
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                usage = new TrafficUsage(reader.GetInt64(0), reader.GetInt64(1));
            }

            LastError = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastError = $"Failed to read history: {ex.Message}";
            _logger.LogError(ex, "Failed to query lifetime history");
        }

        return Task.FromResult(usage);
    }

    public Task<int> PruneRawSamplesBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        _writeGate.Wait(cancellationToken);
        try
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM traffic_samples WHERE bucket_start_utc < $cutoff;";
            command.Parameters.AddWithValue("$cutoff", ToUnixSeconds(cutoffUtc));
            var deleted = command.ExecuteNonQuery();
            LastError = null;
            return Task.FromResult(deleted);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastError = $"Failed to compact history: {ex.Message}";
            _logger.LogError(ex, "Failed to prune raw history samples");
            return Task.FromResult(0);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writeGate.Dispose();
    }

    private void Migrate()
    {
        using var connection = Open();
        var version = GetUserVersion(connection);
        if (version >= SchemaVersion)
        {
            return;
        }

        using var transaction = connection.BeginTransaction();

        if (version < 1)
        {
            Execute(connection, transaction,
                "CREATE TABLE IF NOT EXISTS traffic_samples (" +
                "bucket_start_utc INTEGER PRIMARY KEY, " +
                "bucket_duration_seconds INTEGER NOT NULL, " +
                "download_bytes INTEGER NOT NULL, " +
                "upload_bytes INTEGER NOT NULL);");

            Execute(connection, transaction,
                "CREATE TABLE IF NOT EXISTS daily_usage (" +
                "local_date TEXT PRIMARY KEY, " +
                "download_bytes INTEGER NOT NULL, " +
                "upload_bytes INTEGER NOT NULL);");
        }

        Execute(connection, transaction, $"PRAGMA user_version = {SchemaVersion};");
        transaction.Commit();
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long GetUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    private static long ToUnixSeconds(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
}
