using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TrafficLens.Network.Adapters;
using TrafficLens.Network.Collectors;
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
        return services;
    }
}