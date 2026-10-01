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

    [Fact]
    public void Format_OrderInvariant_RawByteOrderMatchesDisplayMagnitude()
    {
        // Locks in the raw-byte canonical rule: display formatting must never
        // reorder relative magnitudes. For any pair, if rawBytes(a) < rawBytes(b)
        // then the formatted values must represent a smaller or equal quantity.
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
        // equal or larger quantity. Note 2047 B and 2048 B can both render as
        // "2 KB" (rounding) — the display may collide but must never invert.
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