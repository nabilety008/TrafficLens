using System.Net;
using TrafficLens.Core.Conversion;

namespace TrafficLens.Network.Tests;

public sealed class EndpointFormatterTests
{
    [Fact]
    public void Format_Ipv4_ProducesIpColonPort()
    {
        Assert.Equal("192.168.1.20:443", EndpointFormatter.Format(IPAddress.Parse("192.168.1.20"), 443));
    }

    [Fact]
    public void Format_Ipv6_ProducesBracketedEndpoint()
    {
        Assert.Equal("[2001:db8::1]:443", EndpointFormatter.Format(IPAddress.Parse("2001:db8::1"), 443));
        Assert.Equal("[::1]:5433", EndpointFormatter.Format(IPAddress.IPv6Loopback, 5433));
    }

    [Fact]
    public void Format_NullAddressOrPort_ReturnsEmpty()
    {
        IPAddress? noAddress = null;
        int? noPort = 443;

        Assert.Equal(string.Empty, EndpointFormatter.Format(noAddress, noPort));
        Assert.Equal(string.Empty, EndpointFormatter.Format(IPAddress.Any, (int?)null));
        Assert.Equal(string.Empty, EndpointFormatter.Format(noAddress, (int?)null));
    }

    [Fact]
    public void FormatAddress_RendersIpString()
    {
        Assert.Equal("127.0.0.1", EndpointFormatter.FormatAddress(IPAddress.Loopback));
        Assert.Equal("::1", EndpointFormatter.FormatAddress(IPAddress.IPv6Loopback));
    }

    [Fact]
    public void FormatRemote_SuppressesUnspecifiedPeer()
    {
        Assert.Equal(string.Empty, EndpointFormatter.FormatRemote(IPAddress.Any, 0));
        Assert.Equal(string.Empty, EndpointFormatter.FormatRemote(IPAddress.IPv6Any, 0));
        IPAddress? noAddress = null;
        Assert.Equal(string.Empty, EndpointFormatter.FormatRemote(noAddress, 443));
        Assert.Equal("162.159.140.220:443", EndpointFormatter.FormatRemote(IPAddress.Parse("162.159.140.220"), 443));
    }
}