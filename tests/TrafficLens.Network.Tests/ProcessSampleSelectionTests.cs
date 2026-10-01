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
    public void TotalTransferredSort_CrossUnitBoundaries_RankByRawBytes()
    {
        // Raw-byte ordering must hold across display-unit boundaries:
        // 900 B < 1.5 KB < 12 KB < 800 KB < 1.2 MB even though the formatted
        // numeric components (900, 1.5, 12, 800, 1.2) would sort differently.
        const long kb = 1024;
        const long mb = 1024 * 1024;
        var samples = new List<ProcessTrafficSample>
        {
            Sample(1, "bytes", 100, downBytes: 900),               // 900 B
            Sample(2, "kb-small", 200, downBytes: (long)(1.5 * kb)),  // 1.5 KB
            Sample(3, "kb-mid", 300, downBytes: 12 * kb),             // 12 KB
            Sample(4, "kb-big", 400, downBytes: 800 * kb),            // 800 KB
            Sample(5, "mb", 500, downBytes: (long)(1.2 * mb))         // 1.2 MB
        };

        samples.Sort(ProcessSampleSort.Create(ProcessSortKey.TotalTransferred));

        Assert.Equal(new[] { "mb", "kb-big", "kb-mid", "kb-small", "bytes" },
            samples.Select(s => s.ProcessName).ToArray());
    }

    [Theory]
    [InlineData(1023, 1024)]          // 1023 B must rank below 1 KB
    [InlineData(2047, 2048)]          // 2047 B must rank below 2 KB
    [InlineData(1023L * 1024, 1024L * 1024)]   // 1023 KB must rank below 1 MB
    [InlineData(999L * 1024, 1024L * 1024)]    // 999 KB must rank below 1 MB
    public void TotalTransferredSort_UnitBoundary_LowerRawBytesRanksLower(long smaller, long larger)
    {
        var samples = new List<ProcessTrafficSample>
        {
            Sample(1, "larger", 100, downBytes: larger),
            Sample(2, "smaller", 200, downBytes: smaller)
        };

        samples.Sort(ProcessSampleSort.Create(ProcessSortKey.TotalTransferred));

        Assert.Equal("larger", samples[0].ProcessName);
        Assert.Equal("smaller", samples[1].ProcessName);
    }

    [Fact]
    public void TotalTransferredSort_EqualRawBytesAcrossUnits_TieBreaksStable()
    {
        // 1024 B == 1 KB exactly: same raw value must be treated as equal.
        var samples = new List<ProcessTrafficSample>
        {
            Sample(2, "as-kb", 200, downBytes: 1024),
            Sample(1, "as-bytes", 100, downBytes: 1024)
        };

        samples.Sort(ProcessSampleSort.Create(ProcessSortKey.TotalTransferred));

        Assert.Equal(1, samples[0].ProcessId);
        Assert.Equal(2, samples[1].ProcessId);
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
    public void TopConsumer_UnitBoundary_NeverPrefersLargerFormattedNumber()
    {
        // 900 B/s must never beat 2 KB/s regardless of formatted display text.
        var samples = new List<ProcessTrafficSample>
        {
            Sample(1, "small-bytes", 100, downRate: 900),
            Sample(2, "large-kb", 200, downRate: 2048)
        };

        var top = ProcessSampleSelection.TopConsumer(samples);

        Assert.NotNull(top);
        Assert.Equal("large-kb", top.ProcessName);
    }

    [Fact]
    public void Selections_ReturnNullWhenNoSamples()
    {
        Assert.Null(ProcessSampleSelection.TopConsumer(Array.Empty<ProcessTrafficSample>()));
        Assert.Null(ProcessSampleSelection.TopDownload(Array.Empty<ProcessTrafficSample>()));
        Assert.Null(ProcessSampleSelection.TopUpload(Array.Empty<ProcessTrafficSample>()));
    }
}