using System.Windows;
using Microsoft.Extensions.Logging;

namespace TrafficLens.App.Services;

/// <summary>
/// Single, idempotent application exit pipeline (TL-011). Once requested, the
/// exit flag is latched so window close-to-tray logic can no longer intercept
/// anything, the tray icon and floating widget are disposed, and the WPF
/// application shuts down. Everything else (collectors, connection provider,
/// history flush, file logger, DI container) is disposed by the container in
/// <see cref="App.OnExit"/>.
/// </summary>
public sealed class ApplicationExitCoordinator
{
    private readonly ISystemTrayService _trayService;
    private readonly IFloatingWidgetService _floatingWidgetService;
    private readonly ILogger<ApplicationExitCoordinator> _logger;

    public ApplicationExitCoordinator(
        ISystemTrayService trayService,
        IFloatingWidgetService floatingWidgetService,
        ILogger<ApplicationExitCoordinator> logger)
    {
        _trayService = trayService;
        _floatingWidgetService = floatingWidgetService;
        _logger = logger;
    }

    public bool IsExitRequested { get; private set; }

    public void RequestApplicationExit()
    {
        if (IsExitRequested)
        {
            return;
        }

        IsExitRequested = true;
        _logger.LogInformation("Application exit requested");

        _trayService.Dispose();
        _floatingWidgetService.Dispose();

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Application.Current?.Shutdown();
        }
        else
        {
            dispatcher.Invoke(() => Application.Current?.Shutdown());
        }
    }
}