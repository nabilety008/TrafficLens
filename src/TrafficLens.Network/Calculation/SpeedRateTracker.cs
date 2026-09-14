using TrafficLens.Core.Models;

namespace TrafficLens.Network.Calculation;

/// <summary>
/// Maintains one independent rate baseline per adapter and converts cumulative
/// counters into rate samples. Not thread-safe; call from a single producer
/// (the collector's poll loop).
///
/// Re-baseline rules (never emit a spike):
/// - First sample of an adapter only establishes the baseline.
/// - A counter decrease (reset/wrap) re-baselines for the next sample.
/// - An adapter that disappears is pruned; when it returns it re-baselines
///   (reconnect/replacement produces no spike).
/// </summary>
public sealed class SpeedRateTracker
{
    private readonly Dictionary<string, (NetworkCounterSample Sample, long Ticks)> _baselines =
        new(StringComparer.OrdinalIgnoreCase);

    public int BaselineCount
    {
        get
        {
            lock (_baselines)
            {
                return _baselines.Count;
            }
        }
    }

    /// <summary>
    /// Converts the latest cumulative counters into rate samples using the
    /// monotonic tick clock. Ticks not newer than the baseline are treated as
    /// invalid elapsed and the baseline is refreshed instead of emitting.
    /// </summary>
    public IReadOnlyList<NetworkSpeedSample> Track(
        IReadOnlyDictionary<string, NetworkCounterSample> counters,
        long nowTicks,
        double ticksPerSecond)
    {
        var rates = new List<NetworkSpeedSample>(counters.Count);
        List<string>? missing = null;

        lock (_baselines)
        {
            foreach (var (id, sample) in counters)
            {
                if (_baselines.TryGetValue(id, out var baseline)
                    && sample.ReceivedBytes >= baseline.Sample.ReceivedBytes
                    && sample.SentBytes >= baseline.Sample.SentBytes)
                {
                    var elapsedSeconds = (nowTicks - baseline.Ticks) / ticksPerSecond;
                    if (elapsedSeconds > 0)
                    {
                        var rate = NetworkSpeedCalculator.Calculate(baseline.Sample, sample, elapsedSeconds);
                        if (rate is not null)
                        {
                            rates.Add(rate);
                        }
                    }
                }

                _baselines[id] = (sample, nowTicks);
            }

            foreach (var id in _baselines.Keys)
            {
                if (!counters.ContainsKey(id))
                {
                    (missing ??= new List<string>()).Add(id);
                }
            }

            if (missing is not null)
            {
                foreach (var id in missing)
                {
                    _baselines.Remove(id);
                }
            }
        }

        return rates;
    }
}