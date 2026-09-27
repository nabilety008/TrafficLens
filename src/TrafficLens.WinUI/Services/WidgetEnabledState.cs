using TrafficLens.Core.Abstractions;

namespace TrafficLens.WinUI.Services;

/// <summary>
/// The one place the Floating Widget ON/OFF state is decided and persisted.
/// </summary>
/// <remarks>
/// Every entry point - the quick toggle at the top of Settings, the section lower
/// down, and the widget's own native close button - goes through
/// <see cref="SetEnabled"/>. That keeps a single source of truth, so a change from
/// any of them is immediately visible in all of them and survives a restart.
/// <para>
/// <see cref="SetEnabled"/> is idempotent: writing the value that is already stored
/// does nothing and raises no event. That is what makes the close path
/// deterministic. Disabling persists the setting and then hides the window, and
/// hiding does not re-enter this class, so there is no
/// "setting off -> close window -> close event -> write setting" loop.
/// </para>
/// </remarks>
public sealed class WidgetEnabledState
{
    private readonly ISettingsService _settings;

    public WidgetEnabledState(ISettingsService settings)
    {
        _settings = settings;
        IsEnabled = Read(_settings);
    }

    /// <summary>Raised only when the persisted state actually changes.</summary>
    public event EventHandler<bool>? Changed;

    public bool IsEnabled { get; private set; }

    /// <summary>Writes and persists the state. Returns true when it changed.</summary>
    public bool SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled)
        {
            return false;
        }

        IsEnabled = enabled;
        _settings.Set(FloatingWidgetSettings.EnabledKey, enabled.ToString());
        _settings.Save();
        Changed?.Invoke(this, enabled);
        return true;
    }

    /// <summary>Re-reads the persisted value, for callers that may have written it.</summary>
    public void Reload() => IsEnabled = Read(_settings);

    /// <summary>
    /// Whether the widget should be recreated on startup. A disabled widget stays
    /// disabled across restarts.
    /// </summary>
    public static bool ShouldRestore(ISettingsService settings) => Read(settings);

    private static bool Read(ISettingsService settings)
    {
        var value = settings.Get(FloatingWidgetSettings.EnabledKey, bool.FalseString);
        return bool.TryParse(value, out var enabled) && enabled;
    }
}
