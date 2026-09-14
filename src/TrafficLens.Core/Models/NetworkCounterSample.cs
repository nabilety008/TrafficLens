namespace TrafficLens.Core.Models;

public sealed record NetworkCounterSample(
    string AdapterId,
    string AdapterName,
    long ReceivedBytes,
    long SentBytes,
    DateTime Timestamp)
{
    public long TotalBytes => ReceivedBytes + SentBytes;
}