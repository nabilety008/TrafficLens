using TrafficLens.Core.Conversion;

namespace TrafficLens.Network.Tests;

public sealed class DataRateConverterTests
{
    [Fact]
    public void ToBps_IsIdentity()
    {
        Assert.Equal(42.0, DataRateConverter.ToBps(42));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1024)]
    [InlineData(2_621_440)]
    public void ToKBps_IsBinaryMultiple(double bytesPerSecond)
    {
        Assert.Equal(bytesPerSecond / 1024.0, DataRateConverter.ToKBps(bytesPerSecond), 9);
    }

    [Fact]
    public void ToMBps_Converts1MiBPerSecond()
    {
        Assert.Equal(1.0, DataRateConverter.ToMBps(1024 * 1024), 9);
    }

    [Fact]
    public void ToKbps_Converts1Kbps()
    {
        Assert.Equal(1.0, DataRateConverter.ToKbps(125), 9);
    }

    [Fact]
    public void ToMbps_Converts1Mbps()
    {
        Assert.Equal(1.0, DataRateConverter.ToMbps(125_000), 9);
    }

    [Fact]
    public void ToMbps_1MBytePerSecond_Is8Mbps()
    {
        Assert.Equal(8.0, DataRateConverter.ToMbps(1_000_000), 9);
    }

    [Fact]
    public void ToGbps_Converts1Gbps()
    {
        Assert.Equal(1.0, DataRateConverter.ToGbps(125_000_000), 9);
    }
}