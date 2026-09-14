namespace TrafficLens.Network.Process;

public enum TransferDirection
{
    Receive,
    Send
}

public enum NetworkProtocolKind
{
    Tcp,
    Udp
}

public enum IpVersionKind
{
    IPv4,
    IPv6
}

/// <summary>
/// A single decoded kernel-network transfer event fed into the accounting
/// engine. Synthesized in tests and by the ETW collector from the payload PID
/// ("Identifier of the process associated with the request" — never the event
/// header PID, which can be System/Idle for DPC-completed traffic) plus the
/// transfer size field.
/// </summary>
public readonly record struct NetworkTransferEvent(
    int ProcessId,
    TransferDirection Direction,
    int SizeBytes,
    NetworkProtocolKind Protocol,
    IpVersionKind Version,
    long TimestampUtcTicks)
{
    public static NetworkTransferEvent AtTicks(
        int processId,
        TransferDirection direction,
        int sizeBytes,
        NetworkProtocolKind protocol,
        IpVersionKind version,
        long timestampUtcTicks)
        => new(processId, direction, sizeBytes, protocol, version, timestampUtcTicks);
}