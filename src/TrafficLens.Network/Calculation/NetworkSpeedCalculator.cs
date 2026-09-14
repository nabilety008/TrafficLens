using TrafficLens.Core.Models;

namespace TrafficLens.Network.Calculation;

public static class NetworkSpeedCalculator
{
    /// <summary>
    /// Computes a rate sample from two cumulative counter samples and the actual
    /// elapsed time between them (monotonic, not wall-clock).
    /// Returns null when the transition is invalid (zero/negative elapsed, or a
    /// counter decrease that must re-baseline instead of emitting a huge rate).
    /// </summary>
    public static NetworkSpeedSample? Calculate(
        NetworkCounterSample previous,
        NetworkCounterSample current,
        double elapsedSeconds)
    {
        if (elapsedSeconds <= 0)
        {
            return null;
        }

        if (current.ReceivedBytes < previous.ReceivedBytes || current.SentBytes < previous.SentBytes)
        {
            return null;
        }

        var downloadBytesPerSecond = (long)((current.ReceivedBytes - previous.ReceivedBytes) / elapsedSeconds);
        var uploadBytesPerSecond = (long)((current.SentBytes - previous.SentBytes) / elapsedSeconds);

        return new NetworkSpeedSample(
            current.AdapterId,
            current.AdapterName,
            downloadBytesPerSecond,
            uploadBytesPerSecond,
            current.Timestamp);
    }
}