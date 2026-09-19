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
/// <item><c>lifetime_totals</c> — single-row cache of lifetime download/upload bytes.</item>
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
public sealed class SqliteTrafficHistoryRepository : ITrafficHistoryRepository, IDisposable
{
    public const int SchemaVersion = 2;

    private const string DateFormat = "yyyy-MM-dd";

    private readonly string _connectionString;
    private readonly ILogger<SqliteTrafficHistoryRepository> _logger;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private SqliteConnection? _writerConnection;
    private SqliteCommand? _insertCmd;
    private SqliteCommand? _changedCmd;
    private SqliteCommand? _upsertDailyCmd;
    private SqliteCommand? _updateLifetimeCmd;
    private bool _commandsPrepared;
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
            EnsureWriterConnection();
            PrepareCommands();

            using var transaction = _writerConnection!.BeginTransaction();

            _insertCmd!.Transaction = transaction;
            _changedCmd!.Transaction = transaction;
            _upsertDailyCmd!.Transaction = transaction;
            _updateLifetimeCmd!.Transaction = transaction;

            var stored = 0;
            long totalDownload = 0;
            long totalUpload = 0;

            foreach (var bucket in buckets)
            {
                _insertCmd.Parameters["$start"].Value = ToUnixSeconds(bucket.BucketStartUtc);
                _insertCmd.Parameters["$duration"].Value = bucket.DurationSeconds;
                _insertCmd.Parameters["$download"].Value = bucket.DownloadBytes;
                _insertCmd.Parameters["$upload"].Value = bucket.UploadBytes;
                _insertCmd.ExecuteNonQuery();

                var inserted = Convert.ToInt64(_changedCmd.ExecuteScalar(), CultureInfo.InvariantCulture);
                if (inserted == 0)
                {
                    continue;
                }

                var date = HistoryRangeCalculator
                    .LocalDateOf(bucket.BucketStartUtc, timeZone)
                    .ToString(DateFormat, CultureInfo.InvariantCulture);

                _upsertDailyCmd.Parameters["$date"].Value = date;
                _upsertDailyCmd.Parameters["$download"].Value = bucket.DownloadBytes;
                _upsertDailyCmd.Parameters["$upload"].Value = bucket.UploadBytes;
                _upsertDailyCmd.ExecuteNonQuery();

                totalDownload += bucket.DownloadBytes;
                totalUpload += bucket.UploadBytes;
                stored++;
            }

            if (stored > 0)
            {
                _updateLifetimeCmd.Parameters["$download"].Value = totalDownload;
                _updateLifetimeCmd.Parameters["$upload"].Value = totalUpload;
                _updateLifetimeCmd.ExecuteNonQuery();
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
                "SELECT COALESCE(download_bytes, 0), COALESCE(upload_bytes, 0) FROM lifetime_totals;";
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
        _insertCmd?.Dispose();
        _changedCmd?.Dispose();
        _upsertDailyCmd?.Dispose();
        _updateLifetimeCmd?.Dispose();
        _writerConnection?.Dispose();
    }

    private void EnsureWriterConnection()
    {
        if (_writerConnection is not null)
        {
            return;
        }

        _writerConnection = new SqliteConnection(_connectionString);
        _writerConnection.Open();

        using var pragma = _writerConnection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();
    }

    private void PrepareCommands()
    {
        if (_commandsPrepared)
        {
            return;
        }

        var conn = _writerConnection!;

        _insertCmd = conn.CreateCommand();
        _insertCmd.CommandText =
            "INSERT OR IGNORE INTO traffic_samples " +
            "(bucket_start_utc, bucket_duration_seconds, download_bytes, upload_bytes) " +
            "VALUES ($start, $duration, $download, $upload);";
        _insertCmd.Parameters.Add("$start", SqliteType.Integer);
        _insertCmd.Parameters.Add("$duration", SqliteType.Integer);
        _insertCmd.Parameters.Add("$download", SqliteType.Integer);
        _insertCmd.Parameters.Add("$upload", SqliteType.Integer);

        _changedCmd = conn.CreateCommand();
        _changedCmd.CommandText = "SELECT changes();";

        _upsertDailyCmd = conn.CreateCommand();
        _upsertDailyCmd.CommandText =
            "INSERT INTO daily_usage (local_date, download_bytes, upload_bytes) " +
            "VALUES ($date, $download, $upload) " +
            "ON CONFLICT(local_date) DO UPDATE SET " +
            "download_bytes = download_bytes + excluded.download_bytes, " +
            "upload_bytes = upload_bytes + excluded.upload_bytes;";
        _upsertDailyCmd.Parameters.Add("$date", SqliteType.Text);
        _upsertDailyCmd.Parameters.Add("$download", SqliteType.Integer);
        _upsertDailyCmd.Parameters.Add("$upload", SqliteType.Integer);

        _updateLifetimeCmd = conn.CreateCommand();
        _updateLifetimeCmd.CommandText =
            "INSERT INTO lifetime_totals (id, download_bytes, upload_bytes) " +
            "VALUES (1, $download, $upload) " +
            "ON CONFLICT(id) DO UPDATE SET " +
            "download_bytes = download_bytes + excluded.download_bytes, " +
            "upload_bytes = upload_bytes + excluded.upload_bytes;";
        _updateLifetimeCmd.Parameters.Add("$download", SqliteType.Integer);
        _updateLifetimeCmd.Parameters.Add("$upload", SqliteType.Integer);

        _commandsPrepared = true;
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

        if (version < 2)
        {
            Execute(connection, transaction,
                "CREATE TABLE IF NOT EXISTS lifetime_totals (" +
                "id INTEGER PRIMARY KEY CHECK (id = 1), " +
                "download_bytes INTEGER NOT NULL DEFAULT 0, " +
                "upload_bytes INTEGER NOT NULL DEFAULT 0);");

            // Back-fill the lifetime cache from existing v1 history so an
            // upgrade from schema v1 does not lose the pre-upgrade Lifetime
            // total. daily_usage is never pruned and is the exact sum of all
            // persisted buckets, so seeding id=1 from it is lossless.
            Execute(connection, transaction,
                "INSERT OR IGNORE INTO lifetime_totals (id, download_bytes, upload_bytes) " +
                "SELECT 1, COALESCE(SUM(download_bytes), 0), COALESCE(SUM(upload_bytes), 0) " +
                "FROM daily_usage;");
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
