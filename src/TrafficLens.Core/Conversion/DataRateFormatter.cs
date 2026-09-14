using System.Globalization;

namespace TrafficLens.Core.Conversion;

/// <summary>
/// Presentation formatting for data rates. Values are always consumed in
/// bytes/second and localized formatting is deferrable to the caller culture.
/// Unit symbols (B/s, KB/s, MB/s, Mbps) are technical notation and intentionally
/// not translated; only the number's decimal separator/digits follow culture.
/// </summary>
public static class DataRateFormatter
{
    private const double Kilobyte = 1024;
    private const double Megabyte = Kilobyte * Kilobyte;

    public static string FormatAdaptive(long bytesPerSecond, IFormatProvider? provider = null)
    {
        if (bytesPerSecond < 0)
        {
            bytesPerSecond = 0;
        }

        var culture = ResolveCulture(provider);

        var megabytes = bytesPerSecond / Megabyte;
        if (megabytes >= 1)
        {
            return $"{megabytes.ToString("0.##", culture)} MB/s";
        }

        var kilobytes = bytesPerSecond / Kilobyte;
        if (kilobytes >= 1)
        {
            return $"{kilobytes.ToString("0.##", culture)} KB/s";
        }

        return $"{bytesPerSecond} B/s";
    }

    public static string FormatMbps(double bytesPerSecond, IFormatProvider? provider = null)
    {
        if (bytesPerSecond < 0)
        {
            bytesPerSecond = 0;
        }

        var culture = ResolveCulture(provider);
        var mbps = DataRateConverter.ToMbps(bytesPerSecond);
        return $"{mbps.ToString("0.##", culture)} Mbps";
    }

    private static CultureInfo ResolveCulture(IFormatProvider? provider) =>
        provider as CultureInfo ?? CultureInfo.CurrentCulture;
}