using TrafficLens.Core.Models;

namespace TrafficLens.Core.Selection;

/// <summary>
/// Deterministic ordering of <see cref="ProcessTrafficSample"/> for presentation.
/// Numeric keys sort descending (biggest first); Name sorts ascending
/// (ordinal, case-insensitive). Ties always resolve to a stable key identity so
/// the order never flickers between equal values. Pure and allocation-free per
/// comparison; never touches collector state.
/// </summary>
public static class ProcessSampleSort
{
    public static int Compare(ProcessSortKey key, ProcessTrafficSample left, ProcessTrafficSample right)
    {
        int primary;
        switch (key)
        {
            case ProcessSortKey.TotalRate:
                primary = right.TotalBytesPerSecond.CompareTo(left.TotalBytesPerSecond);
                break;
            case ProcessSortKey.DownloadRate:
                primary = right.DownloadBytesPerSecond.CompareTo(left.DownloadBytesPerSecond);
                break;
            case ProcessSortKey.UploadRate:
                primary = right.UploadBytesPerSecond.CompareTo(left.UploadBytesPerSecond);
                break;
            case ProcessSortKey.TotalTransferred:
                primary = right.TotalBytes.CompareTo(left.TotalBytes);
                break;
            case ProcessSortKey.Downloaded:
                primary = right.DownloadBytes.CompareTo(left.DownloadBytes);
                break;
            case ProcessSortKey.Uploaded:
                primary = right.UploadBytes.CompareTo(left.UploadBytes);
                break;
            case ProcessSortKey.Name:
                primary = string.Compare(left.ProcessName, right.ProcessName, StringComparison.OrdinalIgnoreCase);
                break;
            default:
                primary = 0;
                break;
        }

        if (primary != 0)
        {
            return primary;
        }

        var nameCompare = string.Compare(left.ProcessName, right.ProcessName, StringComparison.OrdinalIgnoreCase);
        if (nameCompare != 0)
        {
            return nameCompare;
        }

        var startCompare = left.ProcessStartTimeUtcTicks.CompareTo(right.ProcessStartTimeUtcTicks);
        if (startCompare != 0)
        {
            return startCompare;
        }

        return left.ProcessId.CompareTo(right.ProcessId);
    }

    public static IComparer<ProcessTrafficSample> Create(ProcessSortKey key) =>
        Comparer<ProcessTrafficSample>.Create((left, right) => Compare(key, left, right));
}

/// <summary>
/// Small pure selectors over a snapshot: the currently highest-throughput
/// process (Top Consumer), the fastest downloader and uploader, and whether any
/// process has non-zero current throughput (drives the idle state).
/// </summary>
public static class ProcessSampleSelection
{
    public static bool HasActiveTraffic(IReadOnlyList<ProcessTrafficSample> samples)
    {
        foreach (var sample in samples)
        {
            if (sample.TotalBytesPerSecond > 0)
            {
                return true;
            }
        }

        return false;
    }

    public static ProcessTrafficSample? TopConsumer(IReadOnlyList<ProcessTrafficSample> samples) =>
        MaxBy(samples, static s => s.TotalBytesPerSecond);

    public static ProcessTrafficSample? TopDownload(IReadOnlyList<ProcessTrafficSample> samples) =>
        MaxBy(samples, static s => s.DownloadBytesPerSecond);

    public static ProcessTrafficSample? TopUpload(IReadOnlyList<ProcessTrafficSample> samples) =>
        MaxBy(samples, static s => s.UploadBytesPerSecond);

    private static ProcessTrafficSample? MaxBy(
        IReadOnlyList<ProcessTrafficSample> samples,
        Func<ProcessTrafficSample, double> selector)
    {
        ProcessTrafficSample? best = null;
        var bestValue = double.MinValue;

        foreach (var sample in samples)
        {
            var value = selector(sample);
            if (best is null
                || value > bestValue
                || (value == bestValue && ProcessSampleSort.Compare(ProcessSortKey.TotalRate, sample, best) < 0))
            {
                best = sample;
                bestValue = value;
            }
        }

        return best;
    }
}