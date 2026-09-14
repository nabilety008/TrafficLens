using System.Globalization;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;
using TrafficLens.App.Views;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;
using TrafficLens.Infrastructure.Logging;
using TrafficLens.Infrastructure.Services;
using TrafficLens.Network;

namespace TrafficLens.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
        mainWindow.Show();
        logger.LogInformation("MainWindow shown");

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
        }

        base.OnExit(e);
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
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}