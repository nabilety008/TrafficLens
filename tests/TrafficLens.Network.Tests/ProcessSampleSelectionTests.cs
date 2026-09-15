using TrafficLens.Core.Models;
using TrafficLens.Core.Selection;

namespace TrafficLens.Network.Tests;

public sealed class ProcessSampleSelectionTests
{
    private static ProcessTrafficSample Sample(
        int pid,
        string name,
        long startTicks,
        double downRate = 0,
        double upRate = 0,
        long downBytes = 0,
        long upBytes = 0) =>
        new(pid, startTicks, name, $@"C:\fake\{name}.exe", true, downBytes, upBytes,
            downRate, upRate, DateTime.UtcNow, IsRunning: true);

    [Fact]
    public void DefaultTotalRateSort_OrdersByTotalDescending()
    {
        var samples = new List<ProcessTrafficSample>
        {
            Sample(1, "a", 100, downRate: 200, upRate: 100), // total 300
            Sample(2, "b", 200, downRate: 50, upRate: 50)    // total 100
        };

        var ordered = ProcessSampleSort.Create(ProcessSortKey.TotalRate);
        samples.Sort(ordered);

        Assert.Equal("a", samples[0].ProcessName);
        Assert.Equal("b", samples[1].ProcessName);
    }

    [Fact]
    public void EqualRates_TieBreakByNameThenStartThenPid()
    {
        var samples = new List<ProcessTrafficSample>
        {
            Sample(3, "zeta", 300, downRate: 100),
            Sample(1, "alpha", 100, downRate: 100),
            Sample(2, "alpha", 200, downRate: 100)
        };

        samples.Sort(ProcessSampleSort.Create(ProcessSortKey.TotalRate));

        Assert.Equal("alpha", samples[0].ProcessName);
        Assert.Equal(1, samples[0].ProcessId);
        Assert.Equal("alpha", samples[1].ProcessName);
        Assert.Equal(2, samples[1].ProcessId);
        Assert.Equal("zeta", samples[2].ProcessName);
    }

    [Fact]
    public void DownloadRateSort_OrdersByDownloading()
    {
        var samples = new List<ProcessTrafficSample>
        {
            Sample(1, "slow", 100, downRate: 10),
            Sample(2, "fast", 200, downRate: 500)
        };

        samples.Sort(ProcessSampleSort.Create(ProcessSortKey.DownloadRate));

        Assert.Equal("fast", samples[0].ProcessName);
        Assert.Equal("slow", samples[1].ProcessName);
    }

    [Fact]
    public void UploadRateSort_OrdersByUploading()
    {
        var samples = new List<ProcessTrafficSample>
        {
            Sample(1, "small", 100, upRate: 20),
            Sample(2, "big", 200, upRate: 900)
        };

        samples.Sort(ProcessSampleSort.Create(ProcessSortKey.UploadRate));

        Assert.Equal("big", samples[0].ProcessName);
        Assert.Equal("small", samples[1].ProcessName);
    }

    [Fact]
    public void TotalTransferredSort_OrdersByCumulativeBytes()
    {
        var samples = new List<ProcessTrafficSample>
        {
            Sample(1, "light", 100, downBytes: 1_000),
            Sample(2, "heavy", 200, downBytes: 9_000)
        };

        samples.Sort(ProcessSampleSort.Create(ProcessSortKey.TotalTransferred));

        Assert.Equal("heavy", samples[0].ProcessName);
        Assert.Equal("light", samples[1].ProcessName);
    }

    [Fact]
    public void DownloadedSort_OrdersByDownloadedBytes()
    {
        var samples = new List<ProcessTrafficSample>
        {
            Sample(1, "a", 100, downBytes: 5_000, upBytes: 9_000),
            Sample(2, "b", 200, downBytes: 8_000, upBytes: 1_000)
        };

        samples.Sort(ProcessSampleSort.Create(ProcessSortKey.Downloaded));

        Assert.Equal("b", samples[0].ProcessName);
        Assert.Equal("a", samples[1].ProcessName);
    }

    [Fact]
    public void UploadedSort_OrdersByUploadedBytes()
    {
        var samples = new List<ProcessTrafficSample>
        {
            Sample(1, "a", 100, downBytes: 9_000, upBytes: 1_000),
            Sample(2, "b", 200, downBytes: 5_000, upBytes: 8_000)
        };

        samples.Sort(ProcessSampleSort.Create(ProcessSortKey.Uploaded));

        Assert.Equal("b", samples[0].ProcessName);
        Assert.Equal("a", samples[1].ProcessName);
    }

    [Fact]
    public void NameSort_OrdersAscendingCaseInsensitive()
    {
        var samples = new List<ProcessTrafficSample>
        {
            Sample(1, "Zebra", 100),
            Sample(2, "apple", 200)
        };

        samples.Sort(ProcessSampleSort.Create(ProcessSortKey.Name));

        Assert.Equal("apple", samples[0].ProcessName);
        Assert.Equal("Zebra", samples[1].ProcessName);
    }

    [Fact]
    public void HasActiveTraffic_IsFalseWhenEmptyOrAllIdle()
    {
        Assert.False(ProcessSampleSelection.HasActiveTraffic(Array.Empty<ProcessTrafficSample>()));
        Assert.False(ProcessSampleSelection.HasActiveTraffic(new[] { Sample(1, "a", 100) }));
    }

    [Fact]
    public void HasActiveTraffic_IsTrueWhenAnyProcessHasThroughput()
    {
        var samples = new[]
        {
            Sample(1, "a", 100),
            Sample(2, "b", 200, downRate: 50)
        };

        Assert.True(ProcessSampleSelection.HasActiveTraffic(samples));
    }

    [Fact]
    public void TopConsumer_SelectsHighestTotalThroughput()
    {
        var samples = new List<ProcessTrafficSample>
        {
            Sample(1, "down", 100, downRate: 800, upRate: 10),   // 810
            Sample(2, "up", 200, downRate: 50, upRate: 600),     // 650
            Sample(3, "both", 300, downRate: 400, upRate: 500)   // 900
        };

        Assert.Equal("both", ProcessSampleSelection.TopConsumer(samples)!.ProcessName);
    }

    [Fact]
    public void TopDownload_And_TopUpload_PickCleanWinners()
    {
        var samples = new List<ProcessTrafficSample>
        {
            Sample(1, "down", 100, downRate: 800, upRate: 10),
            Sample(2, "up", 200, downRate: 50, upRate: 600)
        };

        Assert.Equal("down", ProcessSampleSelection.TopDownload(samples)!.ProcessName);
        Assert.Equal("up", ProcessSampleSelection.TopUpload(samples)!.ProcessName);
    }

    [Fact]
    public void Selections_ReturnNullWhenNoSamples()
    {
        Assert.Null(ProcessSampleSelection.TopConsumer(Array.Empty<ProcessTrafficSample>()));
        Assert.Null(ProcessSampleSelection.TopDownload(Array.Empty<ProcessTrafficSample>()));
        Assert.Null(ProcessSampleSelection.TopUpload(Array.Empty<ProcessTrafficSample>()));
    }
}