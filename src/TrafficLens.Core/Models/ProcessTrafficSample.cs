namespace TrafficLens.Core.Models;

public sealed record ProcessTrafficSample(
    int ProcessId,
    string ProcessName,
    string? ExecutablePath,
    long DownloadBytes,
    long UploadBytes,
    DateTime Timestamp)
{
    public long TotalBytes => DownloadBytes + UploadBytes;
}