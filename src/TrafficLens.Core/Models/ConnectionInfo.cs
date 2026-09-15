using System.Net;

namespace TrafficLens.Core.Models;

public enum ConnectionProtocol
{
    Unknown,
    Tcp,
    Udp
}

public enum ConnectionState
{
    Unknown,
    Closed,
    Listen,
    SynSent,
    SynReceived,
    Established,
    FinWait1,
    FinWait2,
    CloseWait,
    Closing,
    LastAck,
    TimeWait
}

public enum ConnectionAddressFamily
{
    Unknown,
    Ipv4,
    Ipv6
}

/// <summary>
/// One live TCP/UDP endpoint owned by a process, as observed from the Windows
/// IP Helper connection tables. Remote endpoint fields are null when Windows does
/// not expose a meaningful connected remote endpoint (most UDP rows); they are
/// never fabricated. Process identity comes from cached metadata resolution and
/// is never merged across processes by executable name.
/// </summary>
public sealed record ConnectionInfo(
    int ProcessId,
    string? ProcessName,
    ConnectionProtocol Protocol,
    ConnectionAddressFamily AddressFamily,
    IPAddress LocalAddress,
    int LocalPort,
    IPAddress? RemoteAddress,
    int? RemotePort,
    ConnectionState State,
    long ProcessStartTimeUtcTicks = 0,
    string? ExecutablePath = null,
    bool IconAvailable = false,
    DateTime? Timestamp = null);