namespace TrafficLens.Core.History;

/// <summary>
/// One persisted aggregation bucket: the system-wide download/upload bytes that
/// TrafficLens observed during a wall-clock interval starting at
/// <see cref="BucketStartUtc"/>. Buckets are minute-sized (see
/// <see cref="TrafficHistoryAccumulator.BucketSeconds"/>); the final bucket of a
/// session may be shorter than a full minute.
/// </summary>
public sealed record TrafficHistoryBucket(
    DateTime BucketStartUtc,
    int DurationSeconds,
    long DownloadBytes,
    long UploadBytes);
