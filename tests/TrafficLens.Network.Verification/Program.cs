using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TrafficLens.Core.Models;
using TrafficLens.Network.Adapters;
using TrafficLens.Network.Aggregation;
using TrafficLens.Network.Collectors;

// Real Windows verification for TL-003:
//  1. Runs the live collector while generating traffic.
//  2. Prints per-adapter REAL-TIME rates (instantaneous poll rates from
//     SpeedSampleReady/GetCurrentSamples) AND the window-average rate computed
//     from cumulative counter deltas over the measured monotonic elapsed time.
//  3. Prints the tunnel-excluding "Internet Total" aggregate rate.
// Cross-check the window-average against native Get-NetAdapterStatistics sampled
// over the same interval from PowerShell (see docs/NETWORK_COLLECTION.md).

if (OperatingSystem.IsWindows() is false)
{
    Console.Error.WriteLine("Verification requires Windows.");
    return 1;
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

static async Task TrafficSteadyWait(TimeSpan duration)
{
    var until = DateTime.UtcNow + duration;
    while (DateTime.UtcNow < until)
    {
        await Task.Delay(Math.Min(250, (int)(until - DateTime.UtcNow).TotalMilliseconds));
    }
}