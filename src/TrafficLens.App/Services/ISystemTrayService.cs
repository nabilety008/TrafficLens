namespace TrafficLens.App.Services;

/// <summary>
/// Abstraction over the single system tray icon (TL-011). Only window/app
/// concerns are exposed here; the concrete service owns the winforms
/// NotifyIcon, its context menu (Open / Show-Hide Floating Widget /
/// Always on Top / Exit), the runtime-drawn icon, and the one-time
/// close-to-tray balloon. All notifications are raised on the WPF
/// Dispatcher thread.
/// </summary>
public interface ISystemTrayService : IDisposable
{
    event EventHandler? OpenRequested;

    event EventHandler? ExitRequested;

    void Show();

    void ShowFirstCloseToTrayNotice();

    /// <summary>
    /// Raises a system tray balloon notification. A click on the balloon restores
    /// the main window (raises <see cref="OpenRequested"/>). Safe to call when the
    /// tray is disposed: the call is dropped and logged rather than throwing.
    /// </summary>
    void ShowAlert(string title, string message);
}