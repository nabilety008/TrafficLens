using System.Globalization;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrafficLens.App.Infrastructure;
using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;
using TrafficLens.App.Views;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;
using TrafficLens.Core.History;
using TrafficLens.Infrastructure.Logging;
using TrafficLens.Infrastructure.Services;
using TrafficLens.Network;

namespace TrafficLens.App;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "TrafficLens.SingleInstance";
    private const string SingleInstanceActivationEventName = "TrafficLens.SingleInstance.Activate";

    private ServiceProvider? _serviceProvider;
    private SingleInstanceGuard? _singleInstanceGuard;
    private IDisposable? _activationWatch;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceGuard = SingleInstanceGuard.TryAcquire(
            SingleInstanceMutexName,
            SingleInstanceActivationEventName);

        if (!_singleInstanceGuard.IsPrimary)
        {
            _singleInstanceGuard.SignalActivation();
            _singleInstanceGuard.Dispose();
            _singleInstanceGuard = null;
            Shutdown();
            return;
        }

        _activationWatch = _singleInstanceGuard.StartActivationWatcher(OnActivationRequested);

        var startMinimized = e.Args.Length > 0 &&
            e.Args.Any(arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase));

        AppPaths.EnsureDirectories();

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("TrafficLens starting up");

        var localization = _serviceProvider.GetRequiredService<ILocalizationService>() as LocalizationService;
        var settings = _serviceProvider.GetRequiredService<ISettingsService>();

        localization?.SetCulture(settings.Language);
        logger.LogInformation("Culture set to {Culture}", localization?.CurrentCulture.Name);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        if (startMinimized)
        {
            logger.LogInformation("Starting hidden to system tray (--minimized)");
        }
        else
        {
            mainWindow.Show();
            logger.LogInformation("MainWindow shown");
        }

        var collector = _serviceProvider.GetRequiredService<INetworkTrafficCollector>();
        try
        {
            _ = collector.StartAsync(CancellationToken.None);
            logger.LogInformation("Network traffic collector started");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start network traffic collector");
        }

        var processCollector = _serviceProvider.GetRequiredService<IProcessTrafficCollector>();
        try
        {
            _ = processCollector.StartAsync(CancellationToken.None);
            logger.LogInformation("Process traffic collector started");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start process traffic collector");
        }

        var connectionProvider = _serviceProvider.GetRequiredService<IConnectionProvider>();
        try
        {
            _ = connectionProvider.StartAsync(CancellationToken.None);
            logger.LogInformation("Connection provider started");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start connection provider");
        }

        var history = _serviceProvider.GetRequiredService<ITrafficHistoryService>();
        try
        {
            _ = history.StartAsync(CancellationToken.None);
            logger.LogInformation("Traffic history service started");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start traffic history service");
        }

        var widgetService = _serviceProvider.GetRequiredService<IFloatingWidgetService>();
        widgetService.RestoreIfEnabled();

        var exitCoordinator = _serviceProvider.GetRequiredService<ApplicationExitCoordinator>();
        var trayService = _serviceProvider.GetRequiredService<ISystemTrayService>();
        trayService.ExitRequested += (_, _) => exitCoordinator.RequestApplicationExit();
        trayService.Show();

        var alertService = _serviceProvider.GetRequiredService<IAlertService>();
        if (localization is not null)
        {
            alertService.AlertRaised += (_, args) =>
            {
                var notification = new AlertNotification(
                    AlertMessageFormatter.Title(localization),
                    AlertMessageFormatter.Message(localization, args.Alert),
                    args.Alert);
                trayService.ShowAlert(notification.Title, notification.Message);
            };
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _serviceProvider
                ?.GetService<ILogger<App>>()
                ?.LogInformation("TrafficLens exiting");
        }
        finally
        {
            _serviceProvider?.Dispose();
            _activationWatch?.Dispose();
            _activationWatch = null;
            _singleInstanceGuard?.Dispose();
            _singleInstanceGuard = null;
        }

        base.OnExit(e);
    }

    private void OnActivationRequested()
    {
        Dispatcher.InvokeAsync(() =>
        {
            var mainWindow = _serviceProvider?.GetService<MainWindow>();
            if (mainWindow is null)
            {
                return;
            }

            if (mainWindow.WindowState == WindowState.Minimized)
            {
                mainWindow.WindowState = WindowState.Normal;
            }

            mainWindow.Show();
            mainWindow.Activate();
        });
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService>(
            new JsonSettingsService(AppPaths.SettingsFile));

        var localizationService = new LocalizationService();
        services.AddSingleton<ILocalizationService>(localizationService);
        services.AddSingleton<LocalizationService>(localizationService);

        services.AddFileLogging(AppPaths.LogsDirectory);

        services.AddNetworkServices();
        services.AddHistoryServices(AppPaths.DatabaseFile);
        services.AddSingleton<ProcessIconResolver>();
        services.AddSingleton<ApplicationsViewModel>();
        services.AddSingleton<ApplicationsView>();
        services.AddSingleton<ConnectionsViewModel>();
        services.AddSingleton<ConnectionsView>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<HistoryView>();
        services.AddSingleton<IAlertService, AlertService>();
        services.AddSingleton<AlertsViewModel>();
        services.AddSingleton<AlertsView>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SettingsView>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<AboutView>();
        services.AddSingleton<IStartupRegistrationService, StartupRegistrationService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<IFloatingWidgetService, FloatingWidgetService>();
        services.AddSingleton<ISystemTrayService, SystemTrayService>();
        services.AddSingleton<ApplicationExitCoordinator>();
    }
}