using TrafficLens.Core.Alerts;

namespace TrafficLens.App.Services;

public sealed class AlertRaisedEventArgs : EventArgs
{
    public AlertRaisedEventArgs(AlertEvent alert) => Alert = alert;

    public AlertEvent Alert { get; }
}

/// <summary>
/// Evaluates the five alert rules from existing pipeline state only (aggregate
/// rate samples + history snapshots; never direct SQL) and raises
/// <see cref="AlertRaised"/> for each newly triggered rule. Recent alerts are
/// kept in-memory for the session; the tray notification path is a separate
/// subscriber so the service stays UI-independent.
/// </summary>
public interface IAlertService : IDisposable
{
    event EventHandler<AlertRaisedEventArgs>? AlertRaised;

    /// <summary>Newest-first snapshot of alerts raised this session (max 100).</summary>
    IReadOnlyList<AlertEvent> RecentAlerts { get; }

    /// <summary>Reloads the alert configuration from settings (no UI page owns it yet).</summary>
    void RefreshConfig();
}