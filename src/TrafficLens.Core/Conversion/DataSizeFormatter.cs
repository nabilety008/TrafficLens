using System.Globalization;

namespace TrafficLens.Core.Conversion;

/// <summary>
/// Presentation formatting for cumulative byte totals (B .. TB, binary 1024
/// units, mirroring <see cref="DataRateFormatter"/>'s binary-unit choices).
/// Only the number's decimal separator follows the culture; unit symbols are
/// technical notation and intentionally not translated.
/// </summary>
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
            return $"{((double)bytes / Terabyte).ToString("0.##", culture)} TB";
        }

        if (bytes >= Gigabyte)
        {
            return $"{((double)bytes / Gigabyte).ToString("0.##", culture)} GB";
        }

        if (bytes >= Megabyte)
        {
            return $"{((double)bytes / Megabyte).ToString("0.##", culture)} MB";
        }

        if (bytes >= Kilobyte)
        {
            return $"{((double)bytes / Kilobyte).ToString("0.##", culture)} KB";
        }

        return $"{bytes} B";
    }
}