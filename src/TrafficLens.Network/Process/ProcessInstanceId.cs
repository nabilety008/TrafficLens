namespace TrafficLens.Network.Process;

/// <summary>
/// Stable process-instance identity: a process that exits and whose PID is
/// reused by a different application receives a distinct start-time and
/// therefore a distinct identity bucket.
/// </summary>
public readonly record struct ProcessInstanceId(int ProcessId, long StartTimeUtcTicks)
{
    public bool HasStartTime => StartTimeUtcTicks != 0;
}