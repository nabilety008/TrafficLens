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

    [Theory]
    [InlineData(900, "900 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(2047, "1.99 KB")]
    [InlineData(2048, "2 KB")]
    [InlineData(12 * 1024, "12 KB")]
    [InlineData(800 * 1024, "800 KB")]
    [InlineData(1023L * 1024, "1023 KB")]
    [InlineData(1024L * 1024, "1 MB")]
    [InlineData(1023L * 1024 * 1024, "1023 MB")]
    [InlineData(1536L * 1024, "1.5 MB")]
    [InlineData(1024L * 1024 * 1024, "1 GB")]
    [InlineData((long)(1.5 * 1024 * 1024 * 1024), "1.5 GB")]
    public void Format_UsesAdaptiveThreeSignificantDigits(long bytes, string expected)
    {
        Assert.Equal(expected, DataSizeFormatter.Format(bytes, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Format_UnitBoundaries_NoDisplayCollision()
    {
        // The reported ambiguity: 2047 B used to display as "2 KB", identical
        // to 2048 B. Adaptive precision keeps them distinguishable while
        // preserving the binary 1024 convention.
        Assert.NotEqual(
            DataSizeFormatter.Format(2047, CultureInfo.InvariantCulture),
            DataSizeFormatter.Format(2048, CultureInfo.InvariantCulture));
        Assert.NotEqual(
            DataSizeFormatter.Format(1023L * 1024 * 1024, CultureInfo.InvariantCulture),
            DataSizeFormatter.Format(1024L * 1024 * 1024, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Format_AdaptivePrecisionRespectsCultureDecimalSeparator()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        Assert.Equal("1,99 KB", DataSizeFormatter.Format(2047, de));
        Assert.Equal("1,5 KB", DataSizeFormatter.Format(1536, de));
    }

    [Fact]
    public void Format_LargeValueCarriesUnitSuffix()
    {
        var mb = 1024L * 1024 * 10;
        Assert.EndsWith("MB", DataSizeFormatter.Format(mb, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Format_OrderInvariant_RawByteOrderMatchesDisplayMagnitude()
    {
        // Locks in the raw-byte canonical rule: display formatting must never
        // reorder relative magnitudes. For any pair, if rawBytes(a) < rawBytes(b)
        // then the formatted values must represent a smaller or equal quantity.
        // (Since adaptive precision, 2047 B renders "1.99 KB" vs 2048 B "2 KB",
        // so distinct magnitudes are also visually distinguishable.)
        long[] rawBytesAscending =
        {
            0, 1, 900, 1023, 1024, 1536, 2047, 2048,
            999 * 1024, 1023 * 1024, 1024L * 1024, (long)(1.2 * 1024 * 1024),
            1024L * 1024 * 1024
        };

        var formatted = rawBytesAscending
            .Select(b => (Raw: b, Text: DataSizeFormatter.Format(b, CultureInfo.InvariantCulture)))
            .ToList();

        // Strict raw ordering must be preserved (no two distinct raw values here).
        Assert.Equal(formatted.OrderBy(f => f.Raw).Select(f => f.Raw), formatted.Select(f => f.Raw));

        // Re-parsing the formatted numeric component must not invert order:
        // e.g. "900 B" (900) must rank below "1.5 KB" (1536).
        double ParseNumber(string text)
        {
            var unit = text[^2..].Trim() switch
            {
                "KB" => 1024d,
                "MB" => 1024d * 1024,
                "GB" => 1024d * 1024 * 1024,
                "TB" => 1024d * 1024 * 1024 * 1024,
                _ => 1d
            };
            return double.Parse(text.Split(' ')[0], CultureInfo.InvariantCulture) * unit;
        }

        for (var i = 1; i < formatted.Count; i++)
        {
            var previous = ParseNumber(formatted[i - 1].Text);
            var current = ParseNumber(formatted[i].Text);
            Assert.True(previous <= current,
                $"{formatted[i - 1].Text} should not display as greater than {formatted[i].Text}");
        }
    }

    [Theory]
    [InlineData(900, 2 * 1024)]       // 900 B vs 2 KB (the reported example)
    [InlineData(1023, 1024)]          // 1023 B vs 1 KB
    [InlineData(2047, 2048)]          // 2047 B vs 2 KB
    [InlineData(1023L * 1024, 1024L * 1024)]   // 1023 KB vs 1 MB
    [InlineData(999L * 1024, 1024L * 1024)]    // 999 KB vs 1 MB
    public void Format_UnitBoundaries_DisplayMagnitudeNeverInvertsRawOrder(long smaller, long larger)
    {
        // Raw canonical rule: the larger raw byte count may only display as an
        // equal or larger quantity. With adaptive precision 2047 B renders as
        // "1.99 KB" and 2048 B as "2 KB" — no collision, never an inversion.
        var smallerText = DataSizeFormatter.Format(smaller, CultureInfo.InvariantCulture);
        var largerText = DataSizeFormatter.Format(larger, CultureInfo.InvariantCulture);

        double ParseNumber(string text)
        {
            var unit = text[^2..].Trim() switch
            {
                "KB" => 1024d,
                "MB" => 1024d * 1024,
                "GB" => 1024d * 1024 * 1024,
                "TB" => 1024d * 1024 * 1024 * 1024,
                _ => 1d
            };
            return double.Parse(text.Split(' ')[0], CultureInfo.InvariantCulture) * unit;
        }

        Assert.True(ParseNumber(smallerText) <= ParseNumber(largerText),
            $"{smallerText} must not display as greater than {largerText}");
    }
}