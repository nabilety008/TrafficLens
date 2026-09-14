namespace TrafficLens.Core.Graph;

/// <summary>
/// A raw, presentation-friendly time-series sample. Stores only numbers and the
/// observed timestamp — never formatted strings (formatting belongs to the view).
/// </summary>
public sealed record TrafficGraphPoint(
    DateTime Timestamp,
    long DownloadBytesPerSecond,
    long UploadBytesPerSecond);