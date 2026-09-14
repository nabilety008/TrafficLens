using System.Runtime.InteropServices;
using TrafficLens.Core.Models;

namespace TrafficLens.Network.Process;

/// <summary>
/// Per-process network accounting engine. Consumed on the ETW event thread via
/// <see cref="Record"/> (hot path: allocation-free, one lock, dictionary slot
/// add-or-update) and sampled about once per second via <see cref="Snapshot"/>,
/// which performs bounded metadata look-ups, recomputes rates over a sliding
/// monotonic window, prunes idle state and returns the bounded snapshot.
/// Accumulated byte totals are the authoritative data; rates never mutate the
/// counters.
/// </summary>
public sealed class ProcessTrafficAccountingEngine
{
    private const long PidReuseSkewToleranceTicks = 2 * TimeSpan.TicksPerSecond;

    private readonly IProcessMetadataProvider _metadataProvider;
    private readonly int _maxProcesses;
    private readonly double _rateWindowSeconds;
    private readonly long _idleRetentionTicks;
    private readonly long _unresolvedRetryTicks;
    private readonly long _revalidationTicks;
    private readonly object _sync = new();

    private readonly Dictionary<ProcessInstanceId, Counter> _counters = new();
    private readonly Dictionary<int, long> _knownStartTicks = new();
    private long _totalEventsProcessed;
    private IReadOnlyList<ProcessTrafficSample> _lastSamples = Array.Empty<ProcessTrafficSample>();

    public ProcessTrafficAccountingEngine(
        IProcessMetadataProvider metadataProvider,
        int maxProcesses = 4096,
        double rateWindowSeconds = 3.0,
        double idleRetentionSeconds = 120.0,
        double unresolvedRetrySeconds = 10.0,
        double revalidationSeconds = 15.0)
    {
        _metadataProvider = metadataProvider;
        _maxProcesses = Math.Max(1, maxProcesses);
        _rateWindowSeconds = Math.Max(0.5, rateWindowSeconds);
        _idleRetentionTicks = (long)(TimeSpan.TicksPerSecond * Math.Max(1.0, idleRetentionSeconds));
        _unresolvedRetryTicks = (long)(TimeSpan.TicksPerSecond * Math.Max(0.5, unresolvedRetrySeconds));
        _revalidationTicks = (long)(TimeSpan.TicksPerSecond * Math.Max(1.0, revalidationSeconds));
    }

    public int ProcessCount
    {
        get
        {
            lock (_sync)
            {
                return _counters.Count;
            }
        }
    }

    public long TotalEventsProcessed
    {
        get
        {
            lock (_sync)
            {
                return _totalEventsProcessed;
            }
        }
    }

    public IReadOnlyList<ProcessTrafficSample> GetCurrentSamples()
    {
        lock (_sync)
        {
            return _lastSamples;
        }
    }

    /// <summary>
    /// Hot path: attributes one decoded network event to its process. Never
    /// fabricates a PID and never merges an unresolvable event into another
    /// process bucket — unknown identities accumulate under their own bucket
    /// until metadata resolution completes.
    /// </summary>
    public void Record(NetworkTransferEvent transfer)
    {
        var key = MakeKey(transfer.ProcessId);
        lock (_sync)
        {
            ref var counter = ref CollectionsMarshal.GetValueRefOrAddDefault(_counters, key, out _);
            if (counter is null)
            {
                counter = new Counter { FirstSeenUtcTicks = transfer.TimestampUtcTicks };
            }

            counter.Amend(transfer);
            _totalEventsProcessed++;

            if (_counters.Count > _maxProcesses)
            {
                EvictOldestLocked();
            }
        }
    }

    /// <summary>
    /// Produces the bounded current snapshot with monotonic-window rates,
    /// resolving metadata that became due and pruning idle state. Called by the
    /// collector's sampling loop.
    /// </summary>
    public IReadOnlyList<ProcessTrafficSample> Snapshot(long nowTicks, long ticksPerSecond, DateTime nowUtc)
    {
        var nowWallTicks = nowUtc.Ticks;
        var windowTicks = (long)(ticksPerSecond * _rateWindowSeconds);
        List<KeyValuePair<ProcessInstanceId, Counter>> resolveQueue = new();

        List<ProcessTrafficSample> samples;
        lock (_sync)
        {
            foreach (var pair in _counters)
            {
                AccelerateRates(pair.Value, nowTicks, ticksPerSecond, windowTicks);
                if (IsMetadataDue(pair.Value, nowWallTicks))
                {
                    resolveQueue.Add(pair);
                }
            }

            samples = BuildSamplesLocked(nowUtc);
        }

        var results = ResolveMetadata(resolveQueue);

        lock (_sync)
        {
            foreach (var (id, counter, result) in results)
            {
                ApplyResolutionLocked(id, counter, result, nowWallTicks);
            }

            PruneLocked(nowWallTicks);
            samples = BuildSamplesLocked(nowUtc);
            _lastSamples = samples;
        }

        return samples;
    }

    public ProcessProtocolTotals? GetProtocolTotals(ProcessInstanceId identity)
    {
        lock (_sync)
        {
            if (!_counters.TryGetValue(identity, out var counter))
            {
                return null;
            }

            return new ProcessProtocolTotals(
                counter.TcpReceivedBytes,
                counter.TcpSentBytes,
                counter.UdpReceivedBytes,
                counter.UdpSentBytes,
                counter.Ipv4ReceivedBytes,
                counter.Ipv4SentBytes,
                counter.Ipv6ReceivedBytes,
                counter.Ipv6SentBytes);
        }
    }

    /// <summary>Resets all state; used when a collector session restarts.</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _counters.Clear();
            _knownStartTicks.Clear();
            _totalEventsProcessed = 0;
            _lastSamples = Array.Empty<ProcessTrafficSample>();
        }
    }

    private ProcessInstanceId MakeKey(int processId)
    {
        return _knownStartTicks.TryGetValue(processId, out var startTicks)
            ? new ProcessInstanceId(processId, startTicks)
            : new ProcessInstanceId(processId, 0);
    }

    private List<ProcessTrafficSample> BuildSamplesLocked(DateTime nowUtc)
    {
        var samples = new List<ProcessTrafficSample>(_counters.Count);
        foreach (var pair in _counters)
        {
            var counter = pair.Value;
            var name = counter.Metadata?.ProcessName ?? $"<unknown pid {pair.Key.ProcessId}>";
            samples.Add(new ProcessTrafficSample(
                pair.Key.ProcessId,
                pair.Key.StartTimeUtcTicks,
                name,
                counter.Metadata?.ExecutablePath,
                counter.Metadata?.IconAvailable ?? false,
                counter.ReceivedBytes,
                counter.SentBytes,
                counter.DownloadRate,
                counter.UploadRate,
                nowUtc));
        }

        samples.Sort(static (a, b) => b.TotalBytes.CompareTo(a.TotalBytes));
        return samples;
    }

    private static void AccelerateRates(Counter counter, long nowTicks, long ticksPerSecond, long windowTicks)
    {
        var window = counter.RateWindow;
        window.Add((nowTicks, counter.ReceivedBytes, counter.SentBytes));
        var cutoff = nowTicks - windowTicks;
        while (window.Count > 1 && window[0].Ticks < cutoff)
        {
            window.RemoveAt(0);
        }

        if (window.Count >= 2)
        {
            var oldest = window[0];
            var elapsedTicks = nowTicks - (double)oldest.Ticks;
            if (elapsedTicks > 0)
            {
                var elapsedSeconds = elapsedTicks / ticksPerSecond;
                counter.DownloadRate = (counter.ReceivedBytes - oldest.Received) / elapsedSeconds;
                counter.UploadRate = (counter.SentBytes - oldest.Sent) / elapsedSeconds;
            }
        }
    }

    private bool IsMetadataDue(Counter counter, long nowWallTicks)
    {
        if (nowWallTicks < counter.MetadataNextCheckUtcTicks)
        {
            return false;
        }

        return counter.Metadata is not null
            || nowWallTicks - counter.LastSeenUtcTicks <= _idleRetentionTicks;
    }

    private List<(ProcessInstanceId Id, Counter Counter, ProcessMetadataResult Result)> ResolveMetadata(
        List<KeyValuePair<ProcessInstanceId, Counter>> queue)
    {
        var results = new List<(ProcessInstanceId, Counter, ProcessMetadataResult)>(queue.Count);
        foreach (var pair in queue)
        {
            ProcessMetadataResult result;
            try
            {
                result = _metadataProvider.Resolve(pair.Key);
            }
            catch
            {
                result = new ProcessMetadataResult(false, null);
            }

            results.Add((pair.Key, pair.Value, result));
        }

        return results;
    }

    private void ApplyResolutionLocked(
        ProcessInstanceId id,
        Counter counter,
        ProcessMetadataResult result,
        long nowWallTicks)
    {
        if (!result.ProcessExists)
        {
            counter.MetadataNextCheckUtcTicks = nowWallTicks + _unresolvedRetryTicks;
            return;
        }

        var metadata = result.Metadata;
        if (metadata is null)
        {
            // The PID is alive but the requested identity is a different
            // instance (PID reuse): isolate the previous instance's totals and
            // stop attributing to it from now on. Future events are re-keyed
            // once the new instance's start time is learned.
            StopAttributionTo(id);

            counter.MetadataNextCheckUtcTicks = nowWallTicks + _revalidationTicks;
            return;
        }

        var actualStart = metadata.ActualStartTimeUtcTicks;
        if (!id.HasStartTime && actualStart != 0)
        {
            RekeyLocked(id, counter, new ProcessInstanceId(id.ProcessId, actualStart), metadata);
            _knownStartTicks[id.ProcessId] = actualStart;
            counter.MetadataNextCheckUtcTicks = nowWallTicks + _revalidationTicks;
            return;
        }

        counter.Metadata = metadata;
        counter.MetadataNextCheckUtcTicks = nowWallTicks + _revalidationTicks;
    }

    private void StopAttributionTo(ProcessInstanceId id)
    {
        if (_knownStartTicks.TryGetValue(id.ProcessId, out var oldStart)
            && oldStart == id.StartTimeUtcTicks)
        {
            _knownStartTicks.Remove(id.ProcessId);
        }
    }

    private void PruneLocked(long nowWallTicks)
    {
        var tooOld = nowWallTicks - _idleRetentionTicks;
        var removed = new List<ProcessInstanceId>();
        foreach (var pair in _counters)
        {
            if (pair.Value.LastSeenUtcTicks < tooOld)
            {
                removed.Add(pair.Key);
            }
        }

        foreach (var key in removed)
        {
            if (_knownStartTicks.TryGetValue(key.ProcessId, out var start)
                && start == key.StartTimeUtcTicks)
            {
                _knownStartTicks.Remove(key.ProcessId);
            }

            _counters.Remove(key);
        }
    }

    private void RekeyLocked(ProcessInstanceId from, Counter counter, ProcessInstanceId to, ProcessMetadata metadata)
    {
        if (_counters.TryGetValue(to, out var existing))
        {
            existing.Merge(counter);
            if (existing.Metadata is null)
            {
                existing.Metadata = metadata;
            }

            _counters.Remove(from);
        }
        else
        {
            counter.Metadata = metadata;
            _counters.Remove(from);
            _counters[to] = counter;
        }
    }

    private void EvictOldestLocked()
    {
        ProcessInstanceId toEvict = default;
        long oldest = long.MaxValue;
        foreach (var pair in _counters)
        {
            if (pair.Value.LastSeenUtcTicks < oldest)
            {
                oldest = pair.Value.LastSeenUtcTicks;
                toEvict = pair.Key;
            }
        }

        if (oldest != long.MaxValue)
        {
            if (_knownStartTicks.TryGetValue(toEvict.ProcessId, out var start)
                && start == toEvict.StartTimeUtcTicks)
            {
                _knownStartTicks.Remove(toEvict.ProcessId);
            }

            _counters.Remove(toEvict);
        }
    }

    private sealed class Counter
    {
        public long FirstSeenUtcTicks;
        public long LastSeenUtcTicks;
        public long ReceivedBytes;
        public long SentBytes;
        public long TcpReceivedBytes;
        public long TcpSentBytes;
        public long UdpReceivedBytes;
        public long UdpSentBytes;
        public long Ipv4ReceivedBytes;
        public long Ipv4SentBytes;
        public long Ipv6ReceivedBytes;
        public long Ipv6SentBytes;
        public ProcessMetadata? Metadata;
        public long MetadataNextCheckUtcTicks;
        public double DownloadRate;
        public double UploadRate;
        public readonly List<(long Ticks, long Received, long Sent)> RateWindow = new();

        public void Amend(NetworkTransferEvent transfer)
        {
            var isReceive = transfer.Direction == TransferDirection.Receive;
            var size = transfer.SizeBytes;
            if (isReceive)
            {
                ReceivedBytes += size;
            }
            else
            {
                SentBytes += size;
            }

            switch (transfer.Protocol)
            {
                case NetworkProtocolKind.Tcp:
                    if (isReceive)
                    {
                        TcpReceivedBytes += size;
                    }
                    else
                    {
                        TcpSentBytes += size;
                    }

                    break;
                case NetworkProtocolKind.Udp:
                    if (isReceive)
                    {
                        UdpReceivedBytes += size;
                    }
                    else
                    {
                        UdpSentBytes += size;
                    }

                    break;
            }

            switch (transfer.Version)
            {
                case IpVersionKind.IPv4:
                    if (isReceive)
                    {
                        Ipv4ReceivedBytes += size;
                    }
                    else
                    {
                        Ipv4SentBytes += size;
                    }

                    break;
                case IpVersionKind.IPv6:
                    if (isReceive)
                    {
                        Ipv6ReceivedBytes += size;
                    }
                    else
                    {
                        Ipv6SentBytes += size;
                    }

                    break;
            }

            LastSeenUtcTicks = transfer.TimestampUtcTicks;
        }

        public void Merge(Counter other)
        {
            ReceivedBytes += other.ReceivedBytes;
            SentBytes += other.SentBytes;
            TcpReceivedBytes += other.TcpReceivedBytes;
            TcpSentBytes += other.TcpSentBytes;
            UdpReceivedBytes += other.UdpReceivedBytes;
            UdpSentBytes += other.UdpSentBytes;
            Ipv4ReceivedBytes += other.Ipv4ReceivedBytes;
            Ipv4SentBytes += other.Ipv4SentBytes;
            Ipv6ReceivedBytes += other.Ipv6ReceivedBytes;
            Ipv6SentBytes += other.Ipv6SentBytes;
            if (FirstSeenUtcTicks == 0 || other.FirstSeenUtcTicks < FirstSeenUtcTicks)
            {
                FirstSeenUtcTicks = other.FirstSeenUtcTicks;
            }

            if (other.LastSeenUtcTicks > LastSeenUtcTicks)
            {
                LastSeenUtcTicks = other.LastSeenUtcTicks;
            }

            foreach (var (ticks, received, sent) in other.RateWindow)
            {
                var copied = (ticks, received, sent);
                RateWindow.Add(copied);
            }
        }
    }
}