namespace TrafficLens.Core.History;

/// <summary>
/// App-facing history service. Owns the delta accumulation + periodic flush
/// pipeline and serves an immutable <see cref="HistorySnapshot"/> to the UI so
/// the ViewModel never performs SQL or blocks on the database.
/// </summary>
public interface ITrafficHistoryService : IDisposable
{
    /// <summary>Raised (on a background thread) whenever the snapshot changes.</summary>
    event EventHandler? HistoryChanged;

    bool IsAvailable { get; }

    string? LastError { get; }

    /// <summary>Latest cached snapshot; cheap and safe to call on the UI thread.</summary>
    HistorySnapshot GetSnapshot();

    /// <summary>Initializes storage, starts the flush loop, and begins recording.</summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Stops recording and flushes pending usage. Idempotent.</summary>
    Task StopAsync();
}
