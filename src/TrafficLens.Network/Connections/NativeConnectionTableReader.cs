using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace TrafficLens.Network.Connections;

/// <summary>
/// One enumeration from the IP Helper extended tables: raw table buffers for the
/// four family/protocol combinations, plus per-table failure information. A null
/// buffer means that specific table could not be read; <see cref="AnySucceeded"/>
/// tells callers whether at least one table was usable.
/// </summary>
internal sealed record NativeConnectionTables(
    byte[]? TcpV4,
    byte[]? TcpV6,
    byte[]? UdpV4,
    byte[]? UdpV6,
    string? FailureMessage)
{
    public bool AnySucceeded => TcpV4 is not null || TcpV6 is not null || UdpV4 is not null || UdpV6 is not null;
}

internal static class NativeConnectionTableReader
{
    private const uint NoError = 0;
    private const uint ErrorInsufficientBuffer = 122;
    private const uint TcpTableOwnerPidAll = 5;
    private const uint UdpTableOwnerPid = 1;
    private const int InitialBufferBytes = 64 * 1024;

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref uint pdwSize,
        [MarshalAs(UnmanagedType.Bool)] bool bOrder,
        uint ulAf,
        uint tableClass,
        uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(
        IntPtr pUdpTable,
        ref uint pdwSize,
        [MarshalAs(UnmanagedType.Bool)] bool bOrder,
        uint ulAf,
        uint tableClass,
        uint reserved);

    public static NativeConnectionTables ReadAll()
    {
        var failures = new List<string>();
        var tcpV4 = Read(() => CallTcp(AddressFamily.InterNetwork), "GetExtendedTcpTable IPv4", failures);
        var tcpV6 = Read(() => CallTcp(AddressFamily.InterNetworkV6), "GetExtendedTcpTable IPv6", failures);
        var udpV4 = Read(() => CallUdp(AddressFamily.InterNetwork), "GetExtendedUdpTable IPv4", failures);
        var udpV6 = Read(() => CallUdp(AddressFamily.InterNetworkV6), "GetExtendedUdpTable IPv6", failures);

        return new NativeConnectionTables(
            tcpV4,
            tcpV6,
            udpV4,
            udpV6,
            failures.Count == 0 ? null : string.Join("; ", failures));
    }

    private static byte[]? Read(Func<byte[]?> call, string label, List<string> failures)
    {
        try
        {
            var result = call();
            if (result is null)
            {
                failures.Add(label);
            }

            return result;
        }
        catch (Exception ex)
        {
            failures.Add($"{label}: {ex.Message}");
            return null;
        }
    }

    private static byte[]? CallTcp(AddressFamily addressFamily)
    {
        var size = (uint)InitialBufferBytes;
        var handle = Marshal.AllocHGlobal((int)size);
        try
        {
            var result = GetExtendedTcpTable(handle, ref size, false, (uint)addressFamily, TcpTableOwnerPidAll, 0);
            if (result == NoError)
            {
                return Copy(handle, size);
            }

            if (result == ErrorInsufficientBuffer)
            {
                Marshal.FreeHGlobal(handle);
                handle = Marshal.AllocHGlobal((int)size);
                result = GetExtendedTcpTable(handle, ref size, false, (uint)addressFamily, TcpTableOwnerPidAll, 0);
                if (result == NoError)
                {
                    return Copy(handle, size);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(handle);
        }

        return null;
    }

    private static byte[]? CallUdp(AddressFamily addressFamily)
    {
        var size = (uint)InitialBufferBytes;
        var handle = Marshal.AllocHGlobal((int)size);
        try
        {
            var result = GetExtendedUdpTable(handle, ref size, false, (uint)addressFamily, UdpTableOwnerPid, 0);
            if (result == NoError)
            {
                return Copy(handle, size);
            }

            if (result == ErrorInsufficientBuffer)
            {
                Marshal.FreeHGlobal(handle);
                handle = Marshal.AllocHGlobal((int)size);
                result = GetExtendedUdpTable(handle, ref size, false, (uint)addressFamily, UdpTableOwnerPid, 0);
                if (result == NoError)
                {
                    return Copy(handle, size);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(handle);
        }

        return null;
    }

    private static byte[] Copy(IntPtr handle, uint size)
    {
        var bytes = new byte[size];
        Marshal.Copy(handle, bytes, 0, (int)size);
        return bytes;
    }
}