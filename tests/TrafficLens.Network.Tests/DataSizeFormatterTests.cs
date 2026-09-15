using System.Globalization;
using TrafficLens.Core.Conversion;

namespace TrafficLens.Network.Tests;

public sealed class DataSizeFormatterTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1, "1 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1024L * 1024, "1 MB")]
    [InlineData(1024L * 1024 * 1024, "1 GB")]
    [InlineData(1024L * 1024 * 1024 * 1024, "1 TB")]
    public void Format_UsesBinaryUnits(long bytes, string expected)
    {
        Assert.Equal(expected, DataSizeFormatter.Format(bytes, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(-1, "0 B")]
    [InlineData(-1024, "0 B")]
    public void Format_NegativeValuesClampToZero(long bytes, string expected)
    {
        Assert.Equal(expected, DataSizeFormatter.Format(bytes, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Format_RespectsCultureDecimalSeparator()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        Assert.Equal("1,5 KB", DataSizeFormatter.Format(1536, de));
    }

    [Fact]
    public void Format_LargeValueCarriesUnitSuffix()
    {
        var mb = 1024L * 1024 * 10;
        Assert.EndsWith("MB", DataSizeFormatter.Format(mb, CultureInfo.InvariantCulture));
    }
}