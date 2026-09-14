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

        var localization = _serviceProvider.GetRequiredService<ILocalizationService>() as LocalizationService;
        var settings = _serviceProvider.GetRequiredService<ISettingsService>();

        localization?.SetCulture(settings.Language);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
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

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}