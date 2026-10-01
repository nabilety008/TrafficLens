using System.Globalization;

namespace TrafficLens.Core.Conversion;

/// <summary>
/// Presentation formatting for cumulative byte totals (B .. TB, binary 1024
/// units, mirroring <see cref="DataRateFormatter"/>'s binary-unit choices).
/// Only the number's decimal separator follows the culture; unit symbols are
/// technical notation and intentionally not translated.
/// </summary>
/// <remarks>
/// Display only: ranking, sorting and comparisons must always use the raw
/// canonical byte value, never this formatted output.
/// </remarks>
public static class DataSizeFormatter
{
    private const double Kilobyte = 1024;
    private const double Megabyte = Kilobyte * 1024;
    private const double Gigabyte = Megabyte * 1024;
    private const double Terabyte = Gigabyte * 1024;

    public static string Format(long bytes, IFormatProvider? provider = null)
    {
        if (bytes < 0)
        {
            bytes = 0;
        }

        var culture = provider as CultureInfo ?? CultureInfo.CurrentCulture;

        if (bytes >= Terabyte)
        {
            return $"{FormatScaled(bytes / Terabyte, culture)} TB";
        }

        if (bytes >= Gigabyte)
        {
            return $"{FormatScaled(bytes / Gigabyte, culture)} GB";
        }

        if (bytes >= Megabyte)
        {
            return $"{FormatScaled(bytes / Megabyte, culture)} MB";
        }

        if (bytes >= Kilobyte)
        {
            return $"{FormatScaled(bytes / Kilobyte, culture)} KB";
        }

        return $"{bytes} B";
    }

    /// <summary>
    /// Adaptive precision keeping three significant digits: two decimals in
    /// the 1..9 range ("1.99"), one from 10..99 ("12.3"), none from 100 up
    /// ("999"). The scaled value is truncated (never rounded up), so 2047 B
    /// renders as "1.99 KB" instead of colliding with 2048 B's "2 KB";
    /// integral values drop trailing zeros ("2", "1.5").
    /// </summary>
    private static string FormatScaled(double value, CultureInfo culture)
    {
        var truncated = value switch
        {
            < 10 => Math.Floor(value * 100) / 100,
            < 100 => Math.Floor(value * 10) / 10,
            _ => Math.Floor(value)
        };

        return truncated.ToString("0.##", culture);
    }
}
