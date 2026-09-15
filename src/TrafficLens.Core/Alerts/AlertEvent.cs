namespace TrafficLens.Core.Alerts;

/// <summary>
/// Structured record of one alert occurrence. Carries the measured value and the
/// rule threshold so the UI can localize and re-format the message after a
/// culture change (no pre-baked text is stored). <see cref="OccurredUtc"/> is the
/// UTC time the condition was detected.
/// </summary>
public sealed record AlertEvent(
    AlertType Type,
    double Value,
    double Threshold,
    DateTimeOffset OccurredUtc);