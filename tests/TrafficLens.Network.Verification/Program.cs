using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TrafficLens.Network.Adapters;
using TrafficLens.Network.Collectors;

// Runs the real Windows collector for a few seconds while generating traffic,
// then prints cumulative counters (JSON) so they can be cross-checked against
// Windows native adapter statistics (Get-NetAdapterStatistics).

if (OperatingSystem.IsWindows() is false)
{
    Console.Error.WriteLine("Verification requires Windows.");
    return 1;
}

var pollIntervalMs = 500;
var collectSeconds = int.TryParse(Environment.GetEnvironmentVariable("TL_VERIFY_SECONDS"), out var s) ? s : 6;

using var collector = new WindowsNetworkTrafficCollector(
    new NetworkInterfaceSource(),
    NullLogger<WindowsNetworkTrafficCollector>.Instance,
    TimeSpan.FromMilliseconds(pollIntervalMs));

await collector.StartAsync(CancellationToken.None);

var trafficTask = Task.Run(async () =>
{
    // Generate a little real internet/e.g. local traffic so counters move.
    try
    {
        using (var client = new HttpClient())
        {
            await client.GetAsync("https://1.1.1.1/", HttpCompletionOption.ResponseHeadersRead);
        }
    }
    catch
    {
        // Traffic generation is best-effort; counters may move from other apps.
    }
});

await Task.WhenAll(TrafficSteadyWait(TimeSpan.FromSeconds(collectSeconds)), trafficTask);

var counterSamples = collector.GetCurrentCounterSamples();
var adapters = collector.GetCurrentAdapters();
var defaultSnapshot = collector.GetDefaultAdapterSnapshot();

var output = new
{
    capturedAtUtc = DateTime.UtcNow,
    pollIntervalMs,
    defaultAdapterId = defaultSnapshot?.Id,
    adapters = adapters.Select(a =>
    {
        var counter = counterSamples.FirstOrDefault(c => c.AdapterId == a.Id);
        return new
        {
            id = a.Id,
            name = a.Name,
            description = a.Description,
            kind = a.Kind.ToString(),
            isUp = a.IsUp,
            isDefault = a.IsDefault,
            receivedBytes = counter?.ReceivedBytes ?? 0,
            sentBytes = counter?.SentBytes ?? 0,
            linkSpeedBitsPerSecond = a.LinkSpeedBitsPerSecond
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