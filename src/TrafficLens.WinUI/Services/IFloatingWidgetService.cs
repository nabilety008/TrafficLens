namespace TrafficLens.WinUI.Services;

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
