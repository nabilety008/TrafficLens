using System.Net;
using System.Net.NetworkInformation;

namespace TrafficLens.Network.Adapters;

public sealed class NetworkInterfaceSource : INetworkInterfaceSource
{
    public IReadOnlyList<RawAdapterSnapshot> GetAdapters()
    {
        var result = new List<RawAdapterSnapshot>();

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            var isUp = networkInterface.OperationalStatus == OperationalStatus.Up;

            var ipProperties = TryGet(() => networkInterface.GetIPProperties());
            var stats = TryGet(() => networkInterface.GetIPv4Statistics());

            var hasGateway = ipProperties?.GatewayAddresses
                .Any(g => g?.Address is { } ip
                          && !ip.Equals(IPAddress.Any)
                          && !ip.Equals(IPAddress.IPv6Any)) ?? false;

            var unicast = ipProperties?.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                ?.Address?.ToString();

            var speed = isUp
                ? TryGet(() => (long?)networkInterface.Speed)
                : null;

            result.Add(new RawAdapterSnapshot(
                Id: networkInterface.Id,
                Name: networkInterface.Name,
                Description: networkInterface.Description,
                PhysicalAddress: networkInterface.GetPhysicalAddress()?.ToString() ?? string.Empty,
                InterfaceType: networkInterface.NetworkInterfaceType,
                IsUp: isUp,
                HasGateway: hasGateway,
                IpAddress: unicast,
                LinkSpeedBitsPerSecond: speed,
                ReceivedBytes: stats?.BytesReceived ?? 0,
                SentBytes: stats?.BytesSent ?? 0));
        }

        return result;
    }

    private static T TryGet<T>(Func<T> getter)
    {
        try
        {
            return getter();
        }
        catch (Exception)
        {
            // A single interface must never break enumeration (e.g. no IPv4 support).
            return default!;
        }
    }
}