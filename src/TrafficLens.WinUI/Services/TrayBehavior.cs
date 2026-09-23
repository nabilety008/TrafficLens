using TrafficLens.Core.Abstractions;

namespace TrafficLens.WinUI.Services;

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
