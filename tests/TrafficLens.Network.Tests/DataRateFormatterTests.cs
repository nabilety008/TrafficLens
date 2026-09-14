using System.Globalization;
using TrafficLens.Core.Conversion;

namespace TrafficLens.Network.Tests;

public class DataRateFormatterTests
{
    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(512, "512 B/s")]
    [InlineData(1023, "1023 B/s")]
    [InlineData(1024, "1 KB/s")]
    [InlineData(1536, "1.5 KB/s")]
    [InlineData(200_000, "195.31 KB/s")]
    [InlineData(1_048_576, "1 MB/s")]
    [InlineData(3_000_000, "2.86 MB/s")]
    [InlineData(-1, "0 B/s")]
    public void FormatAdaptive_ReturnsExpectedByteRate(long bytesPerSecond, string expected)
    {
        var culture = CultureInfo.InvariantCulture;

        Assert.Equal(expected, DataRateFormatter.FormatAdaptive(bytesPerSecond, culture));
    }

    [Theory]
    [InlineData(0, "0 Mbps")]
    [InlineData(3_000_000, "24 Mbps")]
    [InlineData(3_240_000, "25.92 Mbps")]
    [InlineData(3_360_000, "26.88 Mbps")]
    [InlineData(-100, "0 Mbps")]
    public void FormatMbps_ReturnsExpectedBitRate(double bytesPerSecond, string expected)
    {
        var culture = CultureInfo.InvariantCulture;

        Assert.Equal(expected, DataRateFormatter.FormatMbps(bytesPerSecond, culture));
    }

    [Fact]
    public void FormatAdaptive_RespectsCultureDecimalSeparator()
    {
        var fa = new CultureInfo("fa-IR");

        Assert.Contains("٫", DataRateFormatter.FormatAdaptive(200_000, fa));
    }
}