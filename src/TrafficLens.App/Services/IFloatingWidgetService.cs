namespace TrafficLens.App.Services;

/// <summary>
/// Abstraction over the single floating widget (TL-011). The concrete service
/// owns the always-on-top window, its position/topmost persistence, and
/// idempotent show/hide/toggle behavior; consumers (system tray, coordinator,
/// main view) depend on this interface.
/// </summary>
public interface IFloatingWidgetService : IDisposable
{
    event EventHandler? IsVisibleChanged;

    event EventHandler<bool>? AlwaysOnTopChanged;

    bool IsVisible { get; }

    bool IsAlwaysOnTop { get; }

    void Show();

    void Hide();

    void Toggle();

    void ToggleAlwaysOnTop();

    void SetAlwaysOnTop(bool alwaysOnTop);

    void RestoreIfEnabled();
}