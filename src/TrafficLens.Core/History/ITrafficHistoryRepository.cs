namespace TrafficLens.Core.History;

/// <summary>
/// Durable storage for TrafficLens-observed global usage. Implemented by SQLite
/// in the Infrastructure layer; Core stays free of any database technology.
/// Implementations must be safe to call from background threads, must never
/// corrupt or silently delete a user's history database, and must surface
/// failures through <see cref="LastError"/> rather than throwing at callers.
/// </summary>
public interface ITrafficHistoryRepository : IDisposable
{
    /// <summary>True when the database was opened and migrated successfully.</summary>
    bool IsAvailable { get; }

    /// <summary>Last failure detail (no secrets), or null when healthy.</summary>
    string? LastError { get; }

    /// <summary>Opens the database and applies any pending schema migrations.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Atomically appends minute buckets and rolls them into the per-local-day
    /// totals. Idempotent per bucket start time: re-appending an already stored
    /// bucket is a no-op, so a restart can never duplicate committed usage.
    /// Returns the number of new buckets actually stored.
    /// </summary>
    Task<int> AppendBucketsAsync(
        IReadOnlyList<TrafficHistoryBucket> buckets,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken);

    /// <summary>Per-local-day usage for [startInclusive, endExclusive).</summary>
    Task<IReadOnlyDictionary<DateOnly, TrafficUsage>> QueryDailyAsync(
        DateOnly startInclusive,
        DateOnly endExclusive,
        CancellationToken cancellationToken);

    /// <summary>All-time usage totals.</summary>
    Task<TrafficUsage> QueryLifetimeAsync(CancellationToken cancellationToken);

    /// <summary>Deletes raw minute buckets older than the UTC cutoff. Daily totals are kept.</summary>
    Task<int> PruneRawSamplesBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
}
