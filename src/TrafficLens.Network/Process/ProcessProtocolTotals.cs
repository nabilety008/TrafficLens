namespace TrafficLens.Network.Process;

/// <summary>
/// Per-process byte totals broken down by protocol and IP version, exposed for
/// diagnostics and tests. The engine's authoritative totals are
/// <see cref="ProcessTrafficSample.DownloadBytes"/> +
/// <see cref="ProcessTrafficSample.UploadBytes"/>, which equal the sum of the
/// four protocol columns and of the four IP-version columns (tested).
/// </summary>
public sealed record ProcessProtocolTotals(
    long TcpReceivedBytes,
    long TcpSentBytes,
    long UdpReceivedBytes,
    long UdpSentBytes,
    long Ipv4ReceivedBytes,
    long Ipv4SentBytes,
    long Ipv6ReceivedBytes,
    long Ipv6SentBytes)
{
    public long TotalReceivedBytes => TcpReceivedBytes + UdpReceivedBytes;
    public long TotalSentBytes => TcpSentBytes + UdpSentBytes;
}