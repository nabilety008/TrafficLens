using Microsoft.Extensions.Logging;
using TrafficLens.Core.History;
using TrafficLens.Infrastructure.History;

namespace Microsoft.Extensions.DependencyInjection;

public static class HistoryServiceCollectionExtensions
{
    public static IServiceCollection AddHistoryServices(this IServiceCollection services, string databasePath)
    {
        services.AddSingleton<ITrafficHistoryRepository>(sp =>
            new SqliteTrafficHistoryRepository(
                databasePath,
                sp.GetRequiredService<ILogger<SqliteTrafficHistoryRepository>>()));
        services.AddSingleton<ITrafficHistoryService>(sp =>
            new TrafficHistoryService(
                sp.GetRequiredService<TrafficLens.Core.Abstractions.INetworkTrafficCollector>(),
                sp.GetRequiredService<TrafficLens.Core.Abstractions.INetworkAdapterProvider>(),
                sp.GetRequiredService<ITrafficHistoryRepository>(),
                sp.GetRequiredService<ILogger<TrafficHistoryService>>()));
        return services;
    }
}
