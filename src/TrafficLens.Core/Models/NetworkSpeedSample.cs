namespace TrafficLens.Core.Models;

public sealed record NetworkSpeedSample(
    string AdapterId,
    string AdapterName,
    long DownloadBytesPerSecond,
    long UploadBytesPerSecond,
    DateTime Timestamp)
{
    public long TotalBytesPerSecond => DownloadBytesPerSecond + UploadBytesPerSecond;
}