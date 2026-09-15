using System.Net;
using TrafficLens.Core.Models;

namespace TrafficLens.Core.Selection;

public enum ConnectionFilter
{
    All,
    Established,
    Listening,
    Tcp,
    Udp,
    Ipv4,
    Ipv6
}

public enum ConnectionSortKey
{
    Default,
    Process,
    ProcessId,
    Protocol,
    State,
    Local,
    Remote
}

/// <summary>
/// Pure presentation selection over <see cref="ConnectionInfo"/>: filtering,
/// searching and ordering. None of these mutate provider state — the provider
/// keeps its own snapshot regardless of what the UI displays (TL-008 §5/§7/§8).
/// </summary>
public static class ConnectionFiltering
{
    public static bool Matches(ConnectionInfo connection, ConnectionFilter filter) =>
        filter switch
        {
            ConnectionFilter.All => true,
            // Established = active outbound/inbound conversations. UDP has no TCP
            // state, so a UDP row is "active" only when a remote endpoint exists.
            ConnectionFilter.Established =>
                connection.Protocol == ConnectionProtocol.Udp
                    ? connection.RemoteAddress is not null
                    : connection.State == ConnectionState.Established,
            ConnectionFilter.Listening =>
                connection.Protocol == ConnectionProtocol.Tcp && connection.State == ConnectionState.Listen,
            ConnectionFilter.Tcp => connection.Protocol == ConnectionProtocol.Tcp,
            ConnectionFilter.Udp => connection.Protocol == ConnectionProtocol.Udp,
            ConnectionFilter.Ipv4 => connection.AddressFamily == ConnectionAddressFamily.Ipv4,
            ConnectionFilter.Ipv6 => connection.AddressFamily == ConnectionAddressFamily.Ipv6,
            _ => true
        };

    /// <summary>
    /// Lightweight local search over process display name and numeric fields.
    /// Never performs network lookups (TL-008 §7/§11). The process display name
    /// is passed in because it may be a localized "unknown process" fallback.
    /// </summary>
    public static bool MatchesSearch(
        ConnectionInfo connection,
        string query,
        string processDisplayName)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var q = query.Trim();

        if (processDisplayName.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (connection.ProcessId.ToString().Contains(q, StringComparison.Ordinal))
        {
            return true;
        }

        if (connection.LocalAddress.ToString().Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (connection.LocalPort.ToString().Contains(q, StringComparison.Ordinal))
        {
            return true;
        }

        if (connection.RemoteAddress is not null
            && connection.RemoteAddress.ToString().Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var remotePort = connection.RemotePort;
        if (remotePort is not null
            && remotePort.Value.ToString().Contains(q, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }
}

/// <summary>
/// Deterministic ordering of <see cref="ConnectionInfo"/> for presentation. The
/// default order puts useful active connections first (TCP Established or UDP
/// with a remote endpoint), then by process name and PID so the list is stable
/// and easy to scan. Every key falls back to the same deterministic tie-break
/// chain (name, PID, local endpoint, remote endpoint).
/// </summary>
public static class ConnectionSort
{
    public static IComparer<ConnectionInfo> Create(ConnectionSortKey key) =>
        Comparer<ConnectionInfo>.Create((left, right) => Compare(key, left, right));

    public static int Compare(ConnectionSortKey key, ConnectionInfo left, ConnectionInfo right)
    {
        int primary;
        switch (key)
        {
            case ConnectionSortKey.Process:
                primary = string.Compare(Display(left), Display(right), StringComparison.OrdinalIgnoreCase);
                break;
            case ConnectionSortKey.ProcessId:
                primary = left.ProcessId.CompareTo(right.ProcessId);
                break;
            case ConnectionSortKey.Protocol:
                primary = left.Protocol.CompareTo(right.Protocol);
                break;
            case ConnectionSortKey.State:
                primary = StateRank(left).CompareTo(StateRank(right));
                break;
            case ConnectionSortKey.Local:
                primary = CompareEndpoint(left.LocalAddress, left.AddressFamily, left.LocalPort, right);
                break;
            case ConnectionSortKey.Remote:
                primary = CompareRemote(left, right);
                break;
            case ConnectionSortKey.Default:
            default:
                primary = EstablishedRank(left).CompareTo(EstablishedRank(right));
                break;
        }

        if (primary != 0)
        {
            return primary;
        }

        var nameCompare = string.Compare(Display(left), Display(right), StringComparison.OrdinalIgnoreCase);
        if (nameCompare != 0)
        {
            return nameCompare;
        }

        var pidCompare = left.ProcessId.CompareTo(right.ProcessId);
        if (pidCompare != 0)
        {
            return pidCompare;
        }

        var localCompare = CompareEndpoint(left.LocalAddress, left.AddressFamily, left.LocalPort, right);
        if (localCompare != 0)
        {
            return localCompare;
        }

        return CompareRemote(left, right);
    }

    private static string Display(ConnectionInfo connection) =>
        connection.ProcessName ?? string.Empty;

    /// <summary>Active (Established / UDP-connected) is 0; weak states later.</summary>
    private static int EstablishedRank(ConnectionInfo connection) =>
        connection.Protocol == ConnectionProtocol.Udp
            ? connection.RemoteAddress is null ? 2 : 0
            : connection.State == ConnectionState.Established ? 0 : 1;

    private static int StateRank(ConnectionInfo connection)
    {
        if (connection.Protocol == ConnectionProtocol.Udp)
        {
            return connection.RemoteAddress is null ? 100 : 50;
        }

        return connection.State switch
        {
            ConnectionState.Established => 0,
            ConnectionState.SynSent => 1,
            ConnectionState.SynReceived => 2,
            ConnectionState.Listen => 3,
            ConnectionState.FinWait1 => 4,
            ConnectionState.FinWait2 => 5,
            ConnectionState.CloseWait => 6,
            ConnectionState.Closing => 7,
            ConnectionState.LastAck => 8,
            ConnectionState.TimeWait => 9,
            ConnectionState.Closed => 10,
            _ => 11
        };
    }

    private static int CompareRemote(ConnectionInfo left, ConnectionInfo right)
    {
        var leftRemote = left.RemoteAddress;
        var rightRemote = right.RemoteAddress;
        if (leftRemote is null && rightRemote is null)
        {
            return 0;
        }

        if (leftRemote is null)
        {
            return 1;
        }

        if (rightRemote is null)
        {
            return -1;
        }

        var addressCompare = string.Compare(
            leftRemote.ToString(),
            rightRemote.ToString(),
            StringComparison.OrdinalIgnoreCase);
        if (addressCompare != 0)
        {
            return addressCompare;
        }

        return (left.RemotePort ?? 0).CompareTo(right.RemotePort ?? 0);
    }

    private static int CompareEndpoint(
        IPAddress leftAddress,
        ConnectionAddressFamily leftFamily,
        int leftPort,
        ConnectionInfo right)
    {
        var addressCompare = string.Compare(
            leftAddress.ToString(),
            right.LocalAddress.ToString(),
            StringComparison.OrdinalIgnoreCase);
        if (addressCompare != 0)
        {
            return addressCompare;
        }

        var portCompare = leftPort.CompareTo(right.LocalPort);
        if (portCompare != 0)
        {
            return portCompare;
        }

        return leftFamily.CompareTo(right.AddressFamily);
    }
}