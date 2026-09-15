using TrafficLens.Core.Abstractions;

namespace TrafficLens.App.Services;

/// <summary>
/// Settings keys and pure decision helpers for tray behavior (TL-011).
/// Kept together so call sites and tests cannot drift from the actual keys.
/// </summary>
public static class TrayBehavior
{
    public const string MinimizeToTrayKey = "MinimizeToTray";
    public const string CloseToTrayKey = "CloseToTray";
    public const string TrayCloseNoticeShownKey = "TrayCloseNoticeShown";

    public static bool GetMinimizeToTray(ISettingsService settings) =>
        GetBoolean(settings, MinimizeToTrayKey, defaultValue: true);

    public static bool GetCloseToTray(ISettingsService settings) =>
        GetBoolean(settings, CloseToTrayKey, defaultValue: true);

    public static bool ShouldShowFirstCloseNotice(ISettingsService settings) =>
        !GetBoolean(settings, TrayCloseNoticeShownKey, defaultValue: false);

    public static void MarkCloseNoticeShown(ISettingsService settings) =>
        settings.Set(TrayCloseNoticeShownKey, bool.TrueString);

    /// <summary>
    /// Decides what happens when the main window is asked to close. A
    /// requested application exit always wins over close-to-tray so the tray
    /// "Exit" command can never be intercepted by window logic.
    /// </summary>
    public static WindowCloseAction ResolveCloseAction(bool isExitRequested, ISettingsService settings) =>
        isExitRequested || !GetCloseToTray(settings)
            ? WindowCloseAction.Exit
            : WindowCloseAction.HideToTray;

    private static bool GetBoolean(ISettingsService settings, string key, bool defaultValue)
    {
        var value = settings.Get(key, string.Empty);
        return string.IsNullOrEmpty(value) ? defaultValue : bool.TryParse(value, out var parsed) && parsed;
    }
}

public enum WindowCloseAction
{
    Exit,
    HideToTray
}