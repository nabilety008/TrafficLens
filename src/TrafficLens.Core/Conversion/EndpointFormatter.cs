using System.Net;

namespace TrafficLens.Core.Conversion;

/// <summary>
/// Formats technical endpoint strings. IPv6 addresses are bracketed
/// (2001:db8::1 =&gt; [2001:db8::1]:443) per RFC 5952. Endpoints are always
/// left-to-right; the view layer forces LTR so technical text never reflows
/// under RTL cultures.
/// </summary>
public static class EndpointFormatter
{
    public static string Format(IPAddress address, int port) =>
        address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? $"[{address}]:{port}"
            : $"{address}:{port}";

    public static string Format(IPAddress? address, int? port) =>
        address is null || port is null
            ? string.Empty
            : Format(address, port.Value);

    /// <summary>
    /// Formats the remote endpoint of a connection. Listening TCP rows and
    /// unconnected UDP rows have no meaningful peer; the native tables report
    /// those as an unspecified address (0.0.0.0 / ::) with port 0, which is
    /// shown as an empty string rather than a misleading "peer".
    /// </summary>
    public static string FormatRemote(IPAddress? address, int? port)
    {
        if (address is null || port is null or 0)
        {
            return string.Empty;
        }

        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return string.Empty;
        }

        return Format(address, port.Value);
    }

    public static string FormatAddress(IPAddress address) => address.ToString();
}