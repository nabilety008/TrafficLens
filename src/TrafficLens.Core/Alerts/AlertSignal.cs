namespace TrafficLens.Core.Alerts;

/// <summary>
/// The engine's output for a single rule whose condition is newly met. Raw
/// values (byte/s or bytes/local-day) only; presentation is the UI's concern.
/// </summary>
public sealed record AlertSignal(AlertType Type, double Value, double Threshold);