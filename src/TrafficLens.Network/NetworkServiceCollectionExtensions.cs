using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TrafficLens.Network.Adapters;
using TrafficLens.Network.Collectors;
using TrafficLens.Network.Connections;
using TrafficLens.Network.Process;
using TrafficLens.Core.Abstractions;

namespace TrafficLens.Network;

public static class NetworkServiceCollectionExtensions
{
    public static IServiceCollection AddNetworkServices(this IServiceCollection services)
    {
        services.AddSingleton<INetworkInterfaceSource, NetworkInterfaceSource>();
        services.AddSingleton<INetworkAdapterProvider, WindowsNetworkAdapterProvider>();
        services.AddSingleton<INetworkTrafficCollector>(sp =>
            new WindowsNetworkTrafficCollector(
                sp.GetRequiredService<INetworkInterfaceSource>(),
                sp.GetRequiredService<ILogger<WindowsNetworkTrafficCollector>>()));
        services.AddSingleton<IProcessTrafficCollector>(sp =>
            new WindowsEtwProcessTrafficCollector(
                sp.GetRequiredService<ILogger<WindowsEtwProcessTrafficCollector>>()));
        services.AddSingleton<IProcessMetadataProvider, WindowsProcessMetadataProvider>();
        services.AddSingleton<IConnectionProvider>(sp =>
            new WindowsConnectionProvider(
                sp.GetRequiredService<IProcessMetadataProvider>(),
                sp.GetRequiredService<ILogger<WindowsConnectionProvider>>()));
        return services;
    }
}