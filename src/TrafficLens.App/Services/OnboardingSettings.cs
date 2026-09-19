using TrafficLens.Core.Abstractions;

namespace TrafficLens.App.Services;

/// <summary>
/// Settings keys and pure decision helpers for the first-run Get Started
/// experience (TL-019). Kept together so call sites and tests cannot drift
/// from the actual behavior.
///
/// Semantics: the onboarding is shown automatically only for a brand-new
/// profile (no settings file existed before this session). Profiles that
/// already exist (for example v0.1.1 upgraders) are treated as completed so
/// they are never nagged on every launch. Dismissing the panel persists
/// completion and writes the settings file, so later launches skip it.
/// </summary>
public static class OnboardingSettings
{
    public const string CompletedKey = "HasCompletedOnboarding";

    public static void MarkCompleted(ISettingsService settings) =>
        settings.Set(CompletedKey, bool.TrueString);

    public static bool ShouldAutoShow(ISettingsService settings) =>
        !settings.SettingsFileExisted && !IsCompleted(settings);

    public static bool IsCompleted(ISettingsService settings) =>
        GetBoolean(settings, CompletedKey, defaultValue: false);

    private static bool GetBoolean(ISettingsService settings, string key, bool defaultValue)
    {
        var value = settings.Get(key, string.Empty);
        return string.IsNullOrEmpty(value) ? defaultValue : bool.TryParse(value, out var parsed) && parsed;
    }
}