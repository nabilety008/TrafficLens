using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Models;

namespace TrafficLens.Network.Process;

/// <summary>
/// Per-process traffic collector backed by a real-time Windows ETW kernel
/// session (Microsoft-Windows-Kernel-Network via the kernel TCP/IP keyword).
/// Event bytes are attributed with the event payload PID and accumulated in a
/// bounded <see cref="ProcessTrafficAccountingEngine"/>; a ~1 s sample loop
/// publishes <see cref="ProcessTrafficSample"/> snapshots.
///
/// KRITICAL correctness point: for kernel network events the EVENT_TRACE_HEADER
/// ProcessId is the thread context the event happened to be logged in (often
/// System/Idle for DPC completion); TraceEvent's KernelTraceEventParser fixes
/// the header ProcessId from the explicit payload PID field ("Identifier of the
/// process associated with the request") before dispatch, which is the true
/// socket owner we attribute to.
///
/// The kernel provider requires elevation (admin/SeSystemProfile or
/// Performance Log Users). Without it the session cannot be enabled: the
/// collector reports <see cref="ProcessTrafficCollectorStatus.PermissionDenied"/>
/// with a message and the host application is never crashed.
/// </summary>
public sealed class WindowsEtwProcessTrafficCollector : IProcessTrafficCollector
{
    private const string SessionName = "TrafficLensProcessTrace";

    private readonly ILogger<WindowsEtwProcessTrafficCollector> _logger;
    private readonly TimeSpan _snapshotInterval;
    private readonly object _sync = new();

    private TraceEventSession? _session;
    private Task? _lifetimeTask;
    private ProcessTrafficAccountingEngine _engine;
    private CancellationTokenSource? _cts;
    private IReadOnlyList<ProcessTrafficSample> _currentSamples = Array.Empty<ProcessTrafficSample>();

    public WindowsEtwProcessTrafficCollector(
        ILogger<WindowsEtwProcessTrafficCollector> logger,
        TimeSpan? snapshotInterval = null)
    {
        _logger = logger;
        _snapshotInterval = snapshotInterval ?? TimeSpan.FromSeconds(1);
        _engine = new ProcessTrafficAccountingEngine(new WindowsProcessMetadataProvider());
    }

    public ProcessTrafficCollectorStatus Status { get; private set; } = ProcessTrafficCollectorStatus.Stopped;

    public string? LastError { get; private set; }

    /// <summary>Per-instance protocol/version totals for diagnostics and tests.</summary>
    public ProcessProtocolTotals? GetProtocolTotals(ProcessInstanceId identity)
    {
        return _engine.GetProtocolTotals(identity);
    }

    public event EventHandler<IReadOnlyList<ProcessTrafficSample>>? SamplesReady;

    public event EventHandler? StatusChanged;

    public IReadOnlyList<ProcessTrafficSample> GetCurrentSamples()
    {
        lock (_sync)
        {
            return _currentSamples;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Task lifetime;
        lock (_sync)
        {
            if (Status is ProcessTrafficCollectorStatus.Starting or ProcessTrafficCollectorStatus.Running)
            {
                return Task.CompletedTask;
            }

            Status = ProcessTrafficCollectorStatus.Starting;
            LastError = null;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _engine = new ProcessTrafficAccountingEngine(new WindowsProcessMetadataProvider());
            lifetime = _lifetimeTask = Task.Run(() => StartCore(_cts.Token));
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
        _logger.LogInformation("Process traffic collector starting (ETW kernel network session)");

        // StartCore runs for the collector's lifetime (session consume loop +
        // snapshot loop); see StopAsync for how it is terminated.
        _ = lifetime;
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        lock (_sync)
        {
            _cts?.Cancel();
        }

        var session = _session;
        if (session is not null)
        {
            try
            {
                session.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop ETW session; disposing anyway");
            }
        }

        var lifetime = _lifetimeTask;
        if (lifetime is not null)
        {
            try
            {
                await lifetime.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ETW collector lifetime loop ended unexpectedly during stop");
            }
        }

        lock (_sync)
        {
            _session?.Dispose();
            _session = null;
            _lifetimeTask = null;
            _cts?.Dispose();
            _cts = null;
            Status = ProcessTrafficCollectorStatus.Stopped;
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
        _logger.LogInformation("Process traffic collector stopped");
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    private void StartCore(CancellationToken cancellationToken)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Per-process ETW collection requires Windows.");
            }

            if (TraceEventSession.IsElevated() != true)
            {
                throw new UnauthorizedAccessException(
                    "Enabling the ETW kernel network provider requires an elevated (Administrator) process.");
            }

            var session = new TraceEventSession(SessionName, null);
            _session = session;

            session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);

            HookNetworkEvents(session.Source.Kernel);

            _ = Task.Run(() => session.Source.Process(), CancellationToken.None);

            var nowTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            _engine.Snapshot(nowTicks, System.Diagnostics.Stopwatch.Frequency, DateTime.UtcNow);

            var becameRunning = false;
            lock (_sync)
            {
                if (Status == ProcessTrafficCollectorStatus.Starting)
                {
                    Status = ProcessTrafficCollectorStatus.Running;
                    becameRunning = true;
                }
            }

            if (becameRunning)
            {
                StatusChanged?.Invoke(this, EventArgs.Empty);
            }

            _logger.LogInformation("Process traffic collector running (elevated ETW kernel network session)");
            RunSnapshotLoop(cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            SetFailed(ProcessTrafficCollectorStatus.PermissionDenied, ex.Message, ex);
            _logger.LogWarning("Process traffic collector cannot start: permission denied ({Message})", ex.Message);
        }
        catch (Exception ex)
        {
            SetFailed(ProcessTrafficCollectorStatus.Failed, ex.Message, ex);
            _logger.LogError(ex, "Process traffic collector failed to start");
        }
    }

    private void SetFailed(ProcessTrafficCollectorStatus status, string message, Exception? ex)
    {
        lock (_sync)
        {
            var cleanup = _session;
            _session = null;
            LastError = message;
            Status = status;

            try
            {
                cleanup?.Stop();
                cleanup?.Dispose();
            }
            catch
            {
            }
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RunSnapshotLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Task.Delay(_snapshotInterval, cancellationToken).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                var nowTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                var samples = _engine.Snapshot(nowTicks, System.Diagnostics.Stopwatch.Frequency, DateTime.UtcNow);
                lock (_sync)
                {
                    _currentSamples = samples;
                }

                SamplesReady?.Invoke(this, samples);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Process snapshot sampling failed");
            }
        }
    }

    private void OnSourceError(TraceEvent? data)
    {
        if (data is null)
        {
            return;
        }

        _logger.LogWarning("ETW session reported an event loss/error (event {Name}, lost {Lost})",
            data.EventName, data.EventIndex);
    }

    private void HookNetworkEvents(KernelTraceEventParser kernel)
    {
        kernel.TcpIpSend += data => Record(data.ProcessID, TransferDirection.Send, data.size, NetworkProtocolKind.Tcp, IpVersionKind.IPv4);
        kernel.TcpIpRecv += data => Record(data.ProcessID, TransferDirection.Receive, data.size, NetworkProtocolKind.Tcp, IpVersionKind.IPv4);
        kernel.TcpIpSendIPV6 += data => Record(data.ProcessID, TransferDirection.Send, data.size, NetworkProtocolKind.Tcp, IpVersionKind.IPv6);
        kernel.TcpIpRecvIPV6 += data => Record(data.ProcessID, TransferDirection.Receive, data.size, NetworkProtocolKind.Tcp, IpVersionKind.IPv6);
        kernel.UdpIpSend += data => Record(data.ProcessID, TransferDirection.Send, data.size, NetworkProtocolKind.Udp, IpVersionKind.IPv4);
        kernel.UdpIpRecv += data => Record(data.ProcessID, TransferDirection.Receive, data.size, NetworkProtocolKind.Udp, IpVersionKind.IPv4);
        kernel.UdpIpSendIPV6 += data => Record(data.ProcessID, TransferDirection.Send, data.size, NetworkProtocolKind.Udp, IpVersionKind.IPv6);
        kernel.UdpIpRecvIPV6 += data => Record(data.ProcessID, TransferDirection.Receive, data.size, NetworkProtocolKind.Udp, IpVersionKind.IPv6);
    }

    private void Record(int processId, TransferDirection direction, int size, NetworkProtocolKind protocol, IpVersionKind version)
    {
        if (size <= 0)
        {
            return;
        }

        _engine.Record(new NetworkTransferEvent(processId, direction, size, protocol, version, DateTime.UtcNow.Ticks));
    }
}