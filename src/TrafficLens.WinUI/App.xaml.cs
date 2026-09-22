using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;
using TrafficLens.Infrastructure.Logging;
using TrafficLens.Infrastructure.Services;

namespace TrafficLens.WinUI;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public App()
    {
        InitializeComponent();
    }

    public static IServiceProvider Services { get; private set; } = null!;

    public Window? MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppPaths.EnsureDirectories();

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("TrafficLens WinUI shell starting up");

        var localization = _serviceProvider.GetRequiredService<ILocalizationService>();
        var settings = _serviceProvider.GetRequiredService<ISettingsService>();
        localization.SetCulture(settings.Language);
        logger.LogInformation("Culture set to {Culture}", localization.CurrentCulture.Name);

        MainWindow = new MainWindow(
            _serviceProvider.GetRequiredService<ILocalizationService>(),
            _serviceProvider.GetRequiredService<ISettingsService>());
        MainWindow.Activate();
        logger.LogInformation("WinUI MainWindow activated");
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService>(
            new JsonSettingsService(AppPaths.SettingsFile));

        var localizationService = new LocalizationService();
        services.AddSingleton<ILocalizationService>(localizationService);
        services.AddSingleton(localizationService);

        services.AddFileLogging(AppPaths.LogsDirectory);
    }
}
