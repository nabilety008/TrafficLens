using System.Net;

namespace TrafficLens.Core.Models;

/// <summary>
/// Stable, value-based identity of one connection row, built only from fields
/// Windows actually exposes. TCP rows use the full five-tuple plus owner PID;
/// UDP rows drop the remote endpoint the native table does not provide, so a key
/// never invents identity data. Used to keep rows stable across one-second
/// refreshes and to drop connections that have gone away.
/// </summary>
public sealed record ConnectionKey(
    ConnectionProtocol Protocol,
    ConnectionAddressFamily AddressFamily,
    IPAddress LocalAddress,
    int LocalPort,
    IPAddress? RemoteAddress,
    int? RemotePort,
    int ProcessId)
{
    public static ConnectionKey From(ConnectionInfo connection) =>
        new(
            connection.Protocol,
            connection.AddressFamily,
            connection.LocalAddress,
            connection.LocalPort,
            connection.RemoteAddress,
            connection.RemotePort,
            connection.ProcessId);
}