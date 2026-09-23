using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.History;
using TrafficLens.Core.Localization;
using TrafficLens.Infrastructure.Logging;
using TrafficLens.Infrastructure.Services;
using TrafficLens.Network;
using TrafficLens.WinUI.Infrastructure;
using TrafficLens.WinUI.Services;

namespace TrafficLens.WinUI;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "TrafficLens.SingleInstance";
    private const string SingleInstanceActivationEventName = "TrafficLens.SingleInstance.Activate";

    private ServiceProvider? _serviceProvider;
    private SingleInstanceGuard? _singleInstanceGuard;
    private IDisposable? _activationWatch;
    private ILogger<App>? _logger;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    public static IServicesAccessor Services { get; private set; } = null!;

    public static IServiceProvider ServicesProvider => Services.Provider;

    public MainWindow? MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _singleInstanceGuard = SingleInstanceGuard.TryAcquire(
            SingleInstanceMutexName,
            SingleInstanceActivationEventName);

        if (!_singleInstanceGuard.IsPrimary)
        {
            _singleInstanceGuard.SignalActivation();
            _singleInstanceGuard.Dispose();
            _singleInstanceGuard = null;
            NativeMethods.PostQuitMessage(0);
            return;
        }

        _activationWatch = _singleInstanceGuard.StartActivationWatcher(OnActivationRequested);

        AppPaths.EnsureDirectories();

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        Services = new ServicesAccessor(_serviceProvider);

        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        _logger = logger;
        logger.LogInformation("TrafficLens WinUI shell starting up");

        var localization = _serviceProvider.GetRequiredService<ILocalizationService>();
        var settings = _serviceProvider.GetRequiredService<ISettingsService>();
        localization.SetCulture(settings.Language);
        logger.LogInformation("Culture set to {Culture}", localization.CurrentCulture.Name);

        var exitCoordinator = new ApplicationExitCoordinator(
            _serviceProvider.GetRequiredService<ISystemTrayService>(),
            _serviceProvider.GetRequiredService<IFloatingWidgetService>(),
            _serviceProvider.GetRequiredService<ILogger<ApplicationExitCoordinator>>(),
            ShutdownMainWindow);

        try
        {
            MainWindow = new MainWindow(
                localization,
                settings,
                _serviceProvider.GetRequiredService<ISystemTrayService>(),
                exitCoordinator);
            MainWindow.Activate();
            logger.LogInformation("WinUI MainWindow activated");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create or activate MainWindow");
            throw;
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

        var tray = _serviceProvider.GetRequiredService<ISystemTrayService>();
        tray.ExitRequested += (_, _) => exitCoordinator.RequestApplicationExit();
        tray.Show();

        _serviceProvider.GetRequiredService<IFloatingWidgetService>().RestoreIfEnabled();
    }

    private void OnActivationRequested()
    {
        DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() => MainWindow?.ShowMainWindow());
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled UI exception");
        e.Handled = true;
    }

    private void ShutdownMainWindow()
    {
        DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
        {
            MainWindow?.Close();
            MainWindow = null;
            _serviceProvider?.Dispose();
            _activationWatch?.Dispose();
            _singleInstanceGuard?.Dispose();
        });
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService>(
            new JsonSettingsService(AppPaths.SettingsFile));

        var localizationService = new LocalizationService();
        services.AddSingleton<ILocalizationService>(localizationService);
        services.AddSingleton(localizationService);

        services.AddFileLogging(AppPaths.LogsDirectory);
        services.AddNetworkServices();
        services.AddHistoryServices(AppPaths.DatabaseFile);
        services.AddSingleton(DispatcherQueue.GetForCurrentThread()!);
        services.AddSingleton(sp => new Infrastructure.ProcessIconCache(
            sp.GetRequiredService<DispatcherQueue>()));
        services.AddSingleton<IFloatingWidgetService, FloatingWidgetService>();
        services.AddSingleton<ISystemTrayService, SystemTrayService>();
        services.AddSingleton<DnsResolverService>();
    }

    private sealed class ServicesAccessor : IServicesAccessor
    {
        public ServicesAccessor(IServiceProvider provider) => Provider = provider;

        public IServiceProvider Provider { get; }
    }
}

public interface IServicesAccessor
{
    IServiceProvider Provider { get; }
}

internal static partial class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    internal static extern void PostQuitMessage(int nExitCode);
}
