namespace TrafficLens.Core.History;

/// <summary>
/// One hour slot of the Today hourly series. The slot covers the half-open UTC
/// interval [<see cref="StartUtc"/>, <see cref="EndUtcExclusive"/>) and is
/// labeled by the LOCAL hour at which it starts, so during fall-back two
/// adjacent slots may share the same label (the UTC buckets stay unambiguous).
/// </summary>
public readonly record struct HourlyUsagePoint(
    DateTime StartUtc,
    DateTime EndUtcExclusive,
    int LocalHour,
    long DownloadBytes,
    long UploadBytes)
{
    public long TotalBytes => DownloadBytes + UploadBytes;
}