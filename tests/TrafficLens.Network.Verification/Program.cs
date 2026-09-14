using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TrafficLens.Core.Models;
using TrafficLens.Network.Adapters;
using TrafficLens.Network.Aggregation;
using TrafficLens.Network.Collectors;
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