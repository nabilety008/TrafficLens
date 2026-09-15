using Microsoft.Extensions.Logging;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Models;
using TrafficLens.Network.Process;

namespace TrafficLens.Network.Connections;

/// <summary>
/// Live TCP/UDP connection provider backed by the Windows IP Helper extended
/// tables (GetExtendedTcpTable / GetExtendedUdpTable, TCP+UDP, IPv4+IPv6). No
/// packet capture, WFP, WinDivert or drivers; no privileges are needed to read
/// the tables (observing your own machine's connection list is a standard,
/// non-elevated capability). A background loop samples roughly every second off
/// the UI thread and publishes immutable snapshots. A failed enumeration keeps
/// the last good snapshot and surfaces <see cref="LastError"/>; a single failed
/// family/protocol table does not fail the whole poll. Rows are enriched with
/// resolved process metadata and are never merged across process instances.
/// </summary>
public sealed class WindowsConnectionProvider : IConnectionProvider
{
    private readonly object _sync = new();
    private readonly ConnectionProcessResolver _resolver;
    private readonly ILogger<WindowsConnectionProvider> _logger;
    private readonly TimeSpan _pollInterval;

    private IReadOnlyList<ConnectionInfo> _currentConnections = Array.Empty<ConnectionInfo>();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _disposed;
    private string? _lastError;

    public WindowsConnectionProvider(
        IProcessMetadataProvider processMetadataProvider,
        ILogger<WindowsConnectionProvider> logger,
        TimeSpan? pollInterval = null)
    {
        _resolver = new ConnectionProcessResolver(processMetadataProvider);
        _logger = logger;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
    }

    public event EventHandler<IReadOnlyList<ConnectionInfo>>? ConnectionsChanged;

    public string? LastError
    {
        get
        {
            lock (_sync)
            {
                return _lastError;
            }
        }
    }

    public IReadOnlyList<ConnectionInfo> GetCurrentConnections()
    {
        lock (_sync)
        {
            return _currentConnections.ToArray();
        }
    }

    public Task<IReadOnlyList<ConnectionInfo>> GetActiveConnectionsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => EnumerateOnce(), cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_loopTask is not null && !_loopTask.IsCompleted)
            {
                return Task.CompletedTask;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loopTask = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);
        }

        _logger.LogInformation(
            "Connection provider started (IP Helper tables, poll interval {Interval})",
            _pollInterval);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_sync)
        {
            cts = _cts;
            loop = _loopTask;
            _cts = null;
        }

        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        cts.Dispose();
        _logger.LogInformation("Connection provider stopped");
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        StopAsync().GetAwaiter().GetResult();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Publish(EnumerateOnce());

            try
            {
                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private IReadOnlyList<ConnectionInfo> EnumerateOnce()
    {
        var tables = NativeConnectionTableReader.ReadAll();

        if (!tables.AnySucceeded)
        {
            _logger.LogError(
                "Connection tables unavailable: {Failure}",
                tables.FailureMessage ?? "unknown error");
            lock (_sync)
            {
                _lastError = tables.FailureMessage ?? "Connection tables unavailable";
            }

            return GetCurrentConnections();
        }

        if (tables.FailureMessage is not null)
        {
            _logger.LogWarning(
                "Some connection tables unavailable: {Failure}",
                tables.FailureMessage);
        }

        var rows = new List<ConnectionInfo>(
            (tables.TcpV4?.Length ?? 0) / 24
            + (tables.TcpV6?.Length ?? 0) / 56
            + (tables.UdpV4?.Length ?? 0) / 12
            + (tables.UdpV6?.Length ?? 0) / 28);

        foreach (var connection in Parse(tables))
        {
            rows.Add(ApplyProcessMetadata(connection));
        }

        lock (_sync)
        {
            _lastError = null;
        }

        return rows;
    }

    private static IEnumerable<ConnectionInfo> Parse(NativeConnectionTables tables)
    {
        if (tables.TcpV4 is not null)
        {
            foreach (var row in ConnectionTableParser.ParseTcpV4(tables.TcpV4))
            {
                yield return row;
            }
        }

        if (tables.TcpV6 is not null)
        {
            foreach (var row in ConnectionTableParser.ParseTcpV6(tables.TcpV6))
            {
                yield return row;
            }
        }

        if (tables.UdpV4 is not null)
        {
            foreach (var row in ConnectionTableParser.ParseUdpV4(tables.UdpV4))
            {
                yield return row;
            }
        }

        if (tables.UdpV6 is not null)
        {
            foreach (var row in ConnectionTableParser.ParseUdpV6(tables.UdpV6))
            {
                yield return row;
            }
        }
    }

    private ConnectionInfo ApplyProcessMetadata(ConnectionInfo connection)
    {
        var (metadata, processExists) = _resolver.Resolve(
            connection.ProcessId,
            connection.ProcessStartTimeUtcTicks);

        if (!processExists || metadata is null)
        {
            return connection;
        }

        return connection with
        {
            ProcessName = metadata.ProcessName,
            ExecutablePath = metadata.ExecutablePath,
            IconAvailable = metadata.IconAvailable,
            ProcessStartTimeUtcTicks = metadata.ActualStartTimeUtcTicks
        };
    }

    private void Publish(IReadOnlyList<ConnectionInfo> snapshot)
    {
        lock (_sync)
        {
            _currentConnections = snapshot;
        }

        ConnectionsChanged?.Invoke(this, snapshot);
    }
}