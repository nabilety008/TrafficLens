namespace TrafficLens.Core.Models;

/// <summary>
/// A point-in-time snapshot of one process instance's network accounting.
/// <see cref="ProcessId"/> plus <see cref="ProcessStartTimeUtcTicks"/> form the
/// stable process-instance identity that protects against PID reuse: a reused
/// PID produces a different start-time identity and therefore its own sample.
/// Byte totals are cumulative since the collector session started observing the
/// process; rates are derived over a sliding monotonic window (never an assumed
/// 1-second interval).
/// </summary>
public sealed record ProcessTrafficSample(
    int ProcessId,
    long ProcessStartTimeUtcTicks,
    string ProcessName,
    string? ExecutablePath,
    bool IconAvailable,
    long DownloadBytes,
    long UploadBytes,
    double DownloadBytesPerSecond,
    double UploadBytesPerSecond,
    DateTime Timestamp,
    bool? IsRunning = null)
{
    public long TotalBytes => DownloadBytes + UploadBytes;

    public double TotalBytesPerSecond => DownloadBytesPerSecond + UploadBytesPerSecond;

    public bool HasKnownProcessStart => ProcessStartTimeUtcTicks != 0;
}