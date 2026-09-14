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

public sealed record ConnectionInfo(
    int ProcessId,
    string? ProcessName,
    ConnectionProtocol Protocol,
    IPAddress LocalAddress,
    int LocalPort,
    IPAddress RemoteAddress,
    int RemotePort,
    ConnectionState State);