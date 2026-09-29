using System.Globalization;

namespace TrafficLens.Core.Conversion;

/// <summary>
/// Compact, axis-friendly rate formatting (B/s → KB/s → MB/s → GB/s) for graph
/// scale and hover labels. Uses the same binary 1024 unit conventions as
/// <see cref="DataRateFormatter"/>, but trims small values so axis labels stay
/// short and readable. Numbers stay LTR technical notation; only the decimal
/// separator follows the caller's culture.
/// </summary>
public static class DataRateAxisFormatter
{
    private const double Kilobyte = 1024;
    private const double Megabyte = Kilobyte * Kilobyte;
    private const double Gigabyte = Megabyte * Kilobyte;

    public static string Format(long bytesPerSecond, IFormatProvider? provider = null)
    {
        if (bytesPerSecond < 0)
        {
            bytesPerSecond = 0;
        }

        var culture = provider as CultureInfo ?? CultureInfo.CurrentCulture;

        if (bytesPerSecond >= Gigabyte)
        {
            return $"{((double)bytesPerSecond / Gigabyte).ToString("0.##", culture)} GB/s";
        }

        if (bytesPerSecond >= Megabyte)
        {
            return $"{((double)bytesPerSecond / Megabyte).ToString("0.##", culture)} MB/s";
        }

        if (bytesPerSecond >= Kilobyte)
        {
            return $"{((double)bytesPerSecond / Kilobyte).ToString("0.##", culture)} KB/s";
        }

        return $"{bytesPerSecond} B/s";
    }
}
