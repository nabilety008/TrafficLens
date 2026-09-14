using TrafficLens.Core.Models;
using TrafficLens.Network.Calculation;

namespace TrafficLens.Network.Tests;

public sealed class NetworkSpeedCalculatorTests
{
    private static readonly DateTime T0 = new(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);

    private static NetworkCounterSample C(long received, long sent, DateTime ts) =>
        new("eth0", "Ethernet", received, sent, ts);

    [Fact]
    public void Calculate_ComputesDownloadAndUploadFromDeltas()
    {
        var sample = NetworkSpeedCalculator.Calculate(C(100, 50, T0), C(200, 100, T0.AddSeconds(2)), 2.0);

        Assert.NotNull(sample);
        Assert.Equal(50, sample!.DownloadBytesPerSecond);
        Assert.Equal(25, sample.UploadBytesPerSecond);
        Assert.Equal(75, sample.TotalBytesPerSecond);
        Assert.Equal("eth0", sample.AdapterId);
    }

    [Fact]
    public void Calculate_HandlesNonExactElapsedDuration()
    {
        var sample = NetworkSpeedCalculator.Calculate(C(0, 0, T0), C(500, 250, T0.AddSeconds(1.25)), 1.25);

        Assert.Equal(400, sample!.DownloadBytesPerSecond);
        Assert.Equal(200, sample.UploadBytesPerSecond);
    }

    [Fact]
    public void Calculate_ZeroTraffic_ReturnsZeroRates()
    {
        var sample = NetworkSpeedCalculator.Calculate(C(1000, 800, T0), C(1000, 800, T0.AddSeconds(1)), 1.0);

        Assert.Equal(0, sample!.DownloadBytesPerSecond);
        Assert.Equal(0, sample.UploadBytesPerSecond);
    }

    [Fact]
    public void Calculate_CounterDecrease_ReturnsNull()
    {
        Assert.Null(NetworkSpeedCalculator.Calculate(C(1000, 500, T0), C(800, 600, T0.AddSeconds(1)), 1.0));
        Assert.Null(NetworkSpeedCalculator.Calculate(C(1000, 500, T0), C(1000, 400, T0.AddSeconds(1)), 1.0));
    }

    [Fact]
    public void Calculate_ZeroOrNegativeElapsed_ReturnsNull()
    {
        Assert.Null(NetworkSpeedCalculator.Calculate(C(100, 50, T0), C(200, 100, T0), 0.0));
        Assert.Null(NetworkSpeedCalculator.Calculate(C(100, 50, T0), C(200, 100, T0), -1.0));
    }

    [Fact]
    public void Calculate_MissingElapsedDuration_DoesNotAssumeOneSecond()
    {
        var sample = NetworkSpeedCalculator.Calculate(C(1000, 1000, T0), C(3000, 1000, T0.AddSeconds(4)), 4.0);

        Assert.Equal(500, sample!.DownloadBytesPerSecond);
    }
}