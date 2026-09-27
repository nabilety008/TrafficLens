namespace TrafficLens.WinUI.Services;

public interface IFloatingWidgetService : IDisposable
{
    event EventHandler? IsVisibleChanged;

    event EventHandler<bool>? EnabledChanged;

    event EventHandler<bool>? AlwaysOnTopChanged;

    /// <summary>Whether the widget is enabled. The single source of truth.</summary>
    bool IsEnabled { get; }

    bool IsVisible { get; }

    bool IsAlwaysOnTop { get; }

    void Show();

    void Hide();

    void Toggle();

    void ToggleAlwaysOnTop();

    void SetAlwaysOnTop(bool alwaysOnTop);

    /// <summary>
    /// The only way the widget is turned on or off. Persists the setting and then
    /// shows or hides the window.
    /// </summary>
    void SetEnabled(bool enabled);

    void RestoreIfEnabled();
}
