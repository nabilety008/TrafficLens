using TrafficLens.Core.Models;

namespace TrafficLens.Core.Abstractions;

/// <summary>
/// Live TCP/UDP connection provider. Enumeration happens on a background loop
/// (~1 s); consumers receive immutable snapshots through connection-level
/// metadata only — never native table rows and never packet data. Implementations
/// must keep running (and keep exposing their last good snapshot) even when the
/// underlying API fails; failures surface through <see cref="LastError"/>.
/// </summary>
public interface IConnectionProvider : IDisposable
{
    /// <summary>Published after every poll attempt (success or failure).</summary>
    event EventHandler<IReadOnlyList<ConnectionInfo>>? ConnectionsChanged;

    /// <summary>Non-empty when the most recent enumeration attempt failed.</summary>
    string? LastError { get; }

    IReadOnlyList<ConnectionInfo> GetCurrentConnections();

    /// <summary>One-shot enumeration with an immediate result (call off the UI thread).</summary>
    Task<IReadOnlyList<ConnectionInfo>> GetActiveConnectionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Toggles background enumeration. When disabled the loop idles and the
    /// last snapshot is kept; consumers that only need on-demand data (e.g.
    /// hidden pages) should disable polling to avoid per-second enumeration.
    /// </summary>
    void SetPollingEnabled(bool enabled);

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync();
}