using Microsoft.Extensions.Logging;
using TrafficLens.Core.Abstractions;

namespace TrafficLens.WinUI.Services;

public sealed class ApplicationExitCoordinator
{
    private readonly ISystemTrayService _trayService;
    private readonly IFloatingWidgetService _floatingWidgetService;
    private readonly ILogger<ApplicationExitCoordinator> _logger;
    private readonly Action _shutdown;

    public ApplicationExitCoordinator(
        ISystemTrayService trayService,
        IFloatingWidgetService floatingWidgetService,
        ILogger<ApplicationExitCoordinator> logger,
        Action shutdown)
    {
        _trayService = trayService;
        _floatingWidgetService = floatingWidgetService;
        _logger = logger;
        _shutdown = shutdown;
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
        _shutdown();
    }
}
