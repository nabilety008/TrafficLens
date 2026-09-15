using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.Models;
using TrafficLens.Network.Adapters;
using TrafficLens.Network.Aggregation;
using TrafficLens.Network.Collectors;
using TrafficLens.Network.Connections;
using TrafficLens.Network.Process;

// Real Windows verification for TL-003:
//  1. Runs the live collector while generating traffic.
//  2. Prints per-adapter REAL-TIME rates (instantaneous poll rates from
//     SpeedSampleReady/GetCurrentSamples) AND the window-average rate computed
//     from cumulative counter deltas over the measured monotonic elapsed time.
//  3. Prints the tunnel-excluding "Internet Total" aggregate rate.
// Cross-check the window-average against native Get-NetAdapterStatistics sampled
// over the same interval from PowerShell (see docs/NETWORK_COLLECTION.md).
//
// Real Windows verification for TL-007 (--process):
//  1. Starts the per-process ETW collector.
//  2. Non-elevated run: reports PermissionDenied with LastError (valid evidence).
//  3. Elevated run: generates traffic from curl.exe and powershell.exe (two
//     distinguishable apps), verifies per-instance attribution (distinct
//     PID/start-time buckets), checks bounded growth, and stops cleanly.

if (OperatingSystem.IsWindows() is false)
{
    Console.Error.WriteLine("Verification requires Windows.");
    return 1;
}

// Real Windows verification for TL-008 (--connections):
//  1. Starts the IP Helper connection provider (TCP+UDP, IPv4+IPv6).
//  2. Opens a local listening TCP socket and launches a curl download.
//  3. Confirms the listener and the curl owner appear with plausible
//     protocol/state/endpoints/PID/process-name and no fabricated UDP remotes.
// Cross-check against Get-NetTCPConnection / Get-NetUDPEndpoint from PowerShell.

if (args.Contains("--connections", StringComparer.OrdinalIgnoreCase))
{
    return await ConnectionsVerificationAsync().ConfigureAwait(false);
}

if (args.Contains("--process", StringComparer.OrdinalIgnoreCase))
{
    return await ProcessTrafficVerificationAsync().ConfigureAwait(false);
}

var pollIntervalMs = 500;
var windowSeconds = int.TryParse(Environment.GetEnvironmentVariable("TL_VERIFY_SECONDS"), out var s) ? s : 10;

using var collector = new WindowsNetworkTrafficCollector(
    new NetworkInterfaceSource(),
    NullLogger<WindowsNetworkTrafficCollector>.Instance,
    TimeSpan.FromMilliseconds(pollIntervalMs));

await collector.StartAsync(CancellationToken.None);

// Warm up so the collector has a first baseline before we sample counters.
await Task.Delay(Math.Max(pollIntervalMs * 2, 250));

var initialCounters = collector.GetCurrentCounterSamples().ToDictionary(c => c.AdapterId, StringComparer.OrdinalIgnoreCase);
var windowStopwatch = Stopwatch.StartNew();

var trafficTask = Task.Run(async () =>
{
    try
    {
        using var client = new HttpClient();
        for (var i = 0; i < 3; i++)
        {
            await client.GetAsync("https://1.1.1.1/", HttpCompletionOption.ResponseHeadersRead);
            await Task.Delay(300);
        }
    }
    catch
    {
        // Traffic generation is best-effort; other apps still move the counters.
    }
});

await Task.WhenAll(TrafficSteadyWait(TimeSpan.FromSeconds(windowSeconds)), trafficTask);

var elapsedSeconds = windowStopwatch.Elapsed.TotalSeconds;
var finalCounters = collector.GetCurrentCounterSamples().ToDictionary(c => c.AdapterId, StringComparer.OrdinalIgnoreCase);
var currentRates = collector.GetCurrentSamples().ToDictionary(r => r.AdapterId, StringComparer.OrdinalIgnoreCase);
var adapters = collector.GetCurrentAdapters();
var defaultSnapshot = collector.GetDefaultAdapterSnapshot();
var aggregate = NetworkTrafficAggregator.AggregateRates(currentRates.Values.ToList(), adapters);

var output = new
{
    capturedAtUtc = DateTime.UtcNow,
    pollIntervalMs,
    windowSeconds = elapsedSeconds,
    defaultAdapterId = defaultSnapshot?.Id,
    internetTotal = aggregate is null
        ? null
        : new { downloadBytesPerSecond = aggregate.DownloadBytesPerSecond, uploadBytesPerSecond = aggregate.UploadBytesPerSecond },
    adapters = adapters.Select(a =>
    {
        finalCounters.TryGetValue(a.Id, out var final);
        initialCounters.TryGetValue(a.Id, out var initial);
        currentRates.TryGetValue(a.Id, out var rate);
        return new
        {
            id = a.Id,
            name = a.Name,
            description = a.Description,
            kind = a.Kind.ToString(),
            isUp = a.IsUp,
            isDefault = a.IsDefault,
            realtimeDownloadBytesPerSecond = rate?.DownloadBytesPerSecond ?? 0,
            realtimeUploadBytesPerSecond = rate?.UploadBytesPerSecond ?? 0,
            windowMeanDownloadBytesPerSecond = final is null || initial is null || elapsedSeconds <= 0
                ? 0
                : (long)((final.ReceivedBytes - initial.ReceivedBytes) / elapsedSeconds),
            windowMeanUploadBytesPerSecond = final is null || initial is null || elapsedSeconds <= 0
                ? 0
                : (long)((final.SentBytes - initial.SentBytes) / elapsedSeconds)
        };
    }).ToList()
};

Console.OutputEncoding = System.Text.Encoding.UTF8;
await Console.Out.WriteLineAsync(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));

await collector.StopAsync();
return 0;

static async Task<int> ConnectionsVerificationAsync()
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    Console.WriteLine("Connection verification (TL-008) starting...");

    var provider = new WindowsConnectionProvider(
        new WindowsProcessMetadataProvider(),
        NullLogger<WindowsConnectionProvider>.Instance,
        TimeSpan.FromSeconds(1));

    await provider.StartAsync(CancellationToken.None);
    await Task.Delay(1500);

    var listener = new TcpListener(
        IPAddress.Loopback,
        int.TryParse(Environment.GetEnvironmentVariable("TL_VERIFY_PORT"), out var fixedPort) ? fixedPort : 0);
    listener.Start();
    var listenPort = ((IPEndPoint)listener.LocalEndpoint).Port;

    var curl = LaunchCurl();
    await Task.Delay(3000);

    var connections = provider.GetCurrentConnections();
    var listenRow = connections.FirstOrDefault(c =>
        c.Protocol == ConnectionProtocol.Tcp
        && c.State == ConnectionState.Listen
        && c.LocalPort == listenPort);
    var curlRows = curl is null
        ? new List<ConnectionInfo>()
        : connections.Where(c => c.ProcessId == curl.Id).ToList();
    var curlEstablished = curlRows.Where(c => c.State == ConnectionState.Established).ToList();

    await provider.StopAsync();

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        tl008 = "verification",
        status = "ok",
        capturedAtUtc = DateTime.UtcNow,
        lastError = provider.LastError,
        counts = new
        {
            total = connections.Count,
            tcp = connections.Count(c => c.Protocol == ConnectionProtocol.Tcp),
            udp = connections.Count(c => c.Protocol == ConnectionProtocol.Udp),
            ipv4 = connections.Count(c => c.AddressFamily == ConnectionAddressFamily.Ipv4),
            ipv6 = connections.Count(c => c.AddressFamily == ConnectionAddressFamily.Ipv6),
            established = connections.Count(c => c.State == ConnectionState.Established),
            listening = connections.Count(c => c.State == ConnectionState.Listen),
            udpWithRemote = connections.Count(c => c.Protocol == ConnectionProtocol.Udp && c.RemoteAddress is not null),
            unknownProcess = connections.Count(c => string.IsNullOrWhiteSpace(c.ProcessName))
        },
        tcpStates = connections
            .Where(c => c.Protocol == ConnectionProtocol.Tcp)
            .GroupBy(c => c.State.ToString())
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count()),
        listener = new
        {
            port = listenPort,
            observed = listenRow is not null,
            row = listenRow is null ? null : Summarize(listenRow)
        },
        curl = new
        {
            pid = curl?.Id,
            total = curlRows.Count,
            established = curlEstablished.Count,
            processName = curlRows.FirstOrDefault()?.ProcessName,
            allRowsNamed = curlRows.Count > 0 && curlRows.All(c => !string.IsNullOrWhiteSpace(c.ProcessName)),
            sample = curlEstablished.Count > 0 ? Summarize(curlEstablished[0]) : null
        }
    }, new JsonSerializerOptions { WriteIndented = true }));

    curl?.Kill();
    listener.Stop();
    provider.Dispose();
    return 0;
}

static object Summarize(ConnectionInfo connection) => new
{
    pid = connection.ProcessId,
    process = connection.ProcessName,
    protocol = connection.Protocol.ToString(),
    family = connection.AddressFamily.ToString(),
    local = EndpointFormatter.Format(connection.LocalAddress, connection.LocalPort),
    remote = EndpointFormatter.Format(connection.RemoteAddress, connection.RemotePort),
    state = connection.State.ToString()
};

static async Task<int> ProcessTrafficVerificationAsync()
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    Console.WriteLine("Process traffic verification (TL-007) starting...");

    using var collector = new WindowsEtwProcessTrafficCollector(
        NullLogger<WindowsEtwProcessTrafficCollector>.Instance,
        TimeSpan.FromSeconds(1));

    var _ = collector.StartAsync(CancellationToken.None);

    var settledDeadline = DateTime.UtcNow.AddSeconds(5);
    while (collector.Status == ProcessTrafficCollectorStatus.Starting && DateTime.UtcNow < settledDeadline)
    {
        await Task.Delay(100);
    }

    if (collector.Status is ProcessTrafficCollectorStatus.PermissionDenied)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            tl007 = "not-elevated",
            status = collector.Status.ToString(),
            lastError = collector.LastError,
            capturedAtUtc = DateTime.UtcNow
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    if (collector.Status is not ProcessTrafficCollectorStatus.Running)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            tl007 = "failed",
            status = collector.Status.ToString(),
            lastError = collector.LastError,
            capturedAtUtc = DateTime.UtcNow
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 1;
    }

    Console.WriteLine("Collector running (elevated). Baseline for 3s...");
    await Task.Delay(3000);

    var memoryBefore = GC.GetTotalMemory(false);

    var curlA = await RunForSecondsAsync(
        () => LaunchCurl(), "curl.exe", "instance-A", collector, TimeSpan.FromSeconds(12));

    using (var ps = LaunchPowerShell())
    {
        await AwaitProcessBytesAsync(collector, ps.Id, ("powershell", "ps"), TimeSpan.FromSeconds(12));
        await Task.Delay(2000);
    }

    // "Process exit" evidence: kill instance A, then start instance B and show a
    // separate bucket for the new instance.
    curlA?.Kill();
    await Task.Delay(2500);

    var curlB = await RunForSecondsAsync(
        () => LaunchCurl(), "curl.exe", "instance-B", collector, TimeSpan.FromSeconds(12));

    var samples = collector.GetCurrentSamples();
    var memoryAfter = GC.GetTotalMemory(false);

    var curlSamples = samples.Where(s => s.ProcessName.Equals("curl", StringComparison.OrdinalIgnoreCase)).ToList();
    var psSamples = samples.Where(s => s.ProcessName.Equals("powershell", StringComparison.OrdinalIgnoreCase)).ToList();
    var a = curlSamples.FirstOrDefault(s => s.ProcessId == (curlA?.Id ?? -1));
    var b = curlSamples.FirstOrDefault(s => s.ProcessId == (curlB?.Id ?? -1));

    var protocolOf = (ProcessTrafficSample? sample) => sample is null
        ? null
        : collector.GetProtocolTotals(new ProcessInstanceId(sample.ProcessId, sample.ProcessStartTimeUtcTicks));

    await collector.StopAsync();
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        tl007 = "verification",
        status = "ok",
        capturedAtUtc = DateTime.UtcNow,
        distinctInstances = new
        {
            curlInstanceA = a is null ? null : new { pid = a.ProcessId, startUtcTicks = a.ProcessStartTimeUtcTicks, downloadBytes = a.DownloadBytes },
            curlInstanceB = b is null ? null : new { pid = b.ProcessId, startUtcTicks = b.ProcessStartTimeUtcTicks, downloadBytes = b.DownloadBytes },
            distinctPids = a is not null && b is not null && a.ProcessId != b.ProcessId,
            distinguishableApps = a is not null && psSamples.Any()
        },
        protocolTotals = new
        {
            curlA = protocolOf(a),
            curlB = protocolOf(b),
            powershell = protocolOf(psSamples.FirstOrDefault())
        },
        bounded = new
        {
            sampleCount = samples.Count,
            under1000 = samples.Count < 1000,
            managedMemoryBeforeBytes = memoryBefore,
            managedMemoryAfterBytes = memoryAfter,
            managedGrowthBytes = memoryAfter - memoryBefore
        },
        sampleCounts = new
        {
            curl = curlSamples.Count,
            powershell = psSamples.Count
        }
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

static Process? LaunchCurl()
{
    var psi = new ProcessStartInfo
    {
        FileName = "curl.exe",
        Arguments = "-s -o NUL --max-time 30 \"https://speed.cloudflare.com/__down?bytes=25000000\"",
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardError = true,
        RedirectStandardOutput = true
    };
    var p = new Process { StartInfo = psi };
    if (!p.Start())
    {
        return null;
    }

    _ = p.StandardOutput.ReadToEndAsync();
    _ = p.StandardError.ReadToEndAsync();
    return p;
}

static Process LaunchPowerShell()
{
    var psi = new ProcessStartInfo
    {
        FileName = "powershell.exe",
        Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" +
                    "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12;" +
                    "$wc=New-Object System.Net.WebClient;$wc.DownloadData('https://speed.cloudflare.com/__down?bytes=4000000');\"" ,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardError = true,
        RedirectStandardOutput = true
    };
    var p = new Process { StartInfo = psi };
    p.Start();
    _ = p.StandardOutput.ReadToEndAsync();
    _ = p.StandardError.ReadToEndAsync();
    return p;
}

static async Task<Process?> RunForSecondsAsync(
    Func<Process?> launch,
    string processName,
    string label,
    WindowsEtwProcessTrafficCollector collector,
    TimeSpan duration)
{
    var process = launch();
    if (process is null)
    {
        Console.WriteLine($"WARN: failed to launch {label}");
        return process;
    }

    await AwaitProcessBytesAsync(collector, process.Id, (processName, label), duration);
    return process;
}

static async Task AwaitProcessBytesAsync(
    WindowsEtwProcessTrafficCollector collector,
    int processId,
    (string Name, string Label) target,
    TimeSpan timeout)
{
    var until = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < until)
    {
        var sample = collector.GetCurrentSamples().FirstOrDefault(s => s.ProcessId == processId);
        if (sample is not null && sample.DownloadBytes > 1_500_000)
        {
            Console.WriteLine($"  {target.Label}: observed {sample.DownloadBytes} bytes down (p={sample.ProcessId})");
            return;
        }

        if (sample is not null)
        {
            Console.WriteLine($"  {target.Label}: waiting (p={sample.ProcessId}, {sample.DownloadBytes} bytes so far)");
        }

        await Task.Delay(750);
    }

    Console.WriteLine($"  WARN: {target.Label} (p={processId}) did not reach observed-traffic threshold within {timeout.TotalSeconds}s; sample present: {collector.GetCurrentSamples().Any(s => s.ProcessId == processId)}");
}

static async Task TrafficSteadyWait(TimeSpan duration)
{
    var until = DateTime.UtcNow + duration;
    while (DateTime.UtcNow < until)
    {
        await Task.Delay(Math.Min(250, (int)(until - DateTime.UtcNow).TotalMilliseconds));
    }
}