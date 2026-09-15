using TrafficLens.Core.Alerts;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.Localization;

namespace TrafficLens.App.Services;

/// <summary>
/// The user-facing payload of an alert: a short title and a concise, localized
/// message plus the structured event that produced it.
/// </summary>
public sealed record AlertNotification(string Title, string Message, AlertEvent Alert);

/// <summary>
/// Builds concise, localized alert messages from structured events so recent
/// alerts can be re-rendered after a culture change. Only the number formatting
/// is culture-specific; unit symbols are technical notation and untranslated.
/// </summary>
public static class AlertMessageFormatter
{
    public static string Title(ILocalizationService localization) => localization["AlertTitle"];

    public static string Message(ILocalizationService localization, AlertEvent alert)
    {
        var value = FormatValue(alert, localization.CurrentCulture);
        var threshold = FormatThreshold(alert, localization.CurrentCulture);
        var template = alert.Type switch
        {
            AlertType.HighDownloadSpeed => localization["AlertMsgHighDownloadSpeed"],
            AlertType.HighUploadSpeed => localization["AlertMsgHighUploadSpeed"],
            AlertType.DailyDownloadLimit => localization["AlertMsgDailyDownloadLimit"],
            AlertType.DailyUploadLimit => localization["AlertMsgDailyUploadLimit"],
            AlertType.DailyTotalLimit => localization["AlertMsgDailyTotalLimit"],
            _ => throw new ArgumentOutOfRangeException(nameof(alert), alert.Type, null)
        };
        return string.Format(localization.CurrentCulture, template, value, threshold);
    }

    private static string FormatValue(AlertEvent alert, System.Globalization.CultureInfo culture) =>
        alert.Type.IsSpeedRule()
            ? DataRateFormatter.FormatAdaptive((long)Math.Max(0, Math.Round(alert.Value)), culture)
            : DataSizeFormatter.Format((long)Math.Max(0, Math.Round(alert.Value)), culture);

    private static string FormatThreshold(AlertEvent alert, System.Globalization.CultureInfo culture) =>
        alert.Type.IsSpeedRule()
            ? DataRateFormatter.FormatAdaptive((long)Math.Max(0, Math.Round(alert.Threshold)), culture)
            : DataSizeFormatter.Format((long)Math.Max(0, Math.Round(alert.Threshold)), culture);
}