using Microsoft.Extensions.Logging;
using TrafficLens.Infrastructure.Logging;

namespace Microsoft.Extensions.DependencyInjection;

public static class LoggingServiceCollectionExtensions
{
    public static IServiceCollection AddFileLogging(this IServiceCollection services, string logDirectory)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new FileLoggerProvider(logDirectory));
        });
        return services;
    }
}