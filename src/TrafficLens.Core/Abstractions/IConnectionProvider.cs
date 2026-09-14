using TrafficLens.Core.Models;

namespace TrafficLens.Core.Abstractions;

public interface IConnectionProvider : IDisposable
{
    Task<IReadOnlyList<ConnectionInfo>> GetActiveConnectionsAsync(CancellationToken cancellationToken);
}