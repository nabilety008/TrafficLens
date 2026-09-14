namespace TrafficLens.Core.Conversion;

/// <summary>
/// Numeric unit conversions for data rates. Internal values are always
/// bytes/second; string formatting is deferred to the UI (TL-005).
/// KB/MB use binary multiples (1024); Kbps/Mbps/Gbps use decimal multiples
/// (1000/10^6/10^9 bits) as in conventional networking notation.
/// </summary>
public static class DataRateConverter
{
    private const double Kilobyte = 1024;
    private const double Megabyte = Kilobyte * Kilobyte;

    public static double ToBps(double bytesPerSecond) => bytesPerSecond;

    public static double ToKBps(double bytesPerSecond) => bytesPerSecond / Kilobyte;

    public static double ToMBps(double bytesPerSecond) => bytesPerSecond / Megabyte;

    public static double ToKbps(double bytesPerSecond) => bytesPerSecond * 8 / 1000;

    public static double ToMbps(double bytesPerSecond) => bytesPerSecond * 8 / 1_000_000;

    public static double ToGbps(double bytesPerSecond) => bytesPerSecond * 8 / 1_000_000_000;
}