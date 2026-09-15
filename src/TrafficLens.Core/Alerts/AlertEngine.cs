using TrafficLens.Core.History;

namespace TrafficLens.Core.Alerts;

/// <summary>
/// Pure, deterministic alert evaluation. Holds only in-memory state (armed flags,
/// cooldown deadlines, last-triggered local dates); performs no I/O and owns no
/// timers. Callers feed live aggregate rates and history snapshots.
///
/// Speed semantics: a rule triggers ONLY on an upward crossing (below →
/// at-or-above threshold). While the rate stays at/above the threshold nothing
/// repeats; dropping below re-arms the rule. A cooldown (default 5 minutes) is
/// the minimum interval between triggers of the same rule: a crossing observed
/// while still cooling down is consumed silently and the alert fires on the next
/// eligible re-crossing.
///
/// Daily semantics: a usage limit triggers at most once per LOCAL calendar day
/// (DST-safe, via the supplied time zone, matching the history pipeline's day
/// identity). The triggered local date is retained and can be restored after a
/// restart via <see cref="RestoreTriggeredDates"/>, so a rule already tripped
/// today never re-fires; the next local day automatically re-arms.
///
/// The engine never reads the database and never blocks on anything but its own
/// lock.
/// </summary>
public sealed class AlertEngine
{
    private readonly object _gate = new();
    private readonly Dictionary<AlertType, SpeedRuleState> _speed = new();
    private readonly Dictionary<AlertType, DateOnly?> _lastDailyTrigger = new();

    private TimeZoneInfo _timeZone;
    private Func<DateTimeOffset> _utcNow;
    private AlertConfig _config;

    public AlertEngine(
        AlertConfig config,
        TimeZoneInfo timeZone,
        Func<DateTimeOffset> utcNow)
    {
        _config = config;
        _timeZone = timeZone;
        _utcNow = utcNow;
    }

    public AlertConfig Config
    {
        get
        {
            lock (_gate)
            {
                return _config;
            }
        }
    }

    public void UpdateConfig(AlertConfig config, bool resetSpeedArmed = false)
    {
        lock (_gate)
        {
            _config = config;
            if (resetSpeedArmed)
            {
                _speed.Clear();
            }
        }
    }

    public TimeZoneInfo TimeZone
    {
        get
        {
            lock (_gate)
            {
                return _timeZone;
            }
        }
        set
        {
            lock (_gate)
            {
                _timeZone = value;
            }
        }
    }

    /// <summary>
    /// Evaluates the two speed rules against the aggregate download/upload rates.
    /// Historic-safe: rapid repeated calls are cheap (pure addition and a lock).
    /// </summary>
    public IReadOnlyList<AlertSignal> EvaluateSpeed(double downloadRate, double uploadRate)
    {
        lock (_gate)
        {
            var results = new List<AlertSignal>(2);
            EvaluateSpeedRule(AlertType.HighDownloadSpeed, downloadRate, results);
            EvaluateSpeedRule(AlertType.HighUploadSpeed, uploadRate, results);
            return results;
        }
    }

    /// <summary>
    /// Evaluates the three usage rules against today's totals. When the history
    /// snapshot is unavailable the daily rules are suspended silently (no signal,
    /// no state change) so the engine stays consistent with suspended recording.
    /// </summary>
    public IReadOnlyList<AlertSignal> EvaluateDaily(HistorySnapshot snapshot)
    {
        lock (_gate)
        {
            var results = new List<AlertSignal>(3);
            if (!snapshot.IsAvailable)
            {
                return results;
            }

            var todayDate = HistoryRangeCalculator.LocalDateOf(_utcNow().UtcDateTime, _timeZone);
            EvaluateDailyRule(AlertType.DailyDownloadLimit, snapshot.Today.DownloadBytes, todayDate, results);
            EvaluateDailyRule(AlertType.DailyUploadLimit, snapshot.Today.UploadBytes, todayDate, results);
            EvaluateDailyRule(AlertType.DailyTotalLimit, snapshot.Today.TotalBytes, todayDate, results);
            return results;
        }
    }

    /// <summary>
    /// Snapshot of the per-rule last-triggered local dates, for persistence across
    /// restarts. Only the three daily rules are tracked; null means never triggered.
    /// </summary>
    public IReadOnlyDictionary<AlertType, DateOnly?> TriggeredDailyDates()
    {
        lock (_gate)
        {
            return new Dictionary<AlertType, DateOnly?>(_lastDailyTrigger);
        }
    }

    /// <summary>
    /// Restores previously persisted last-triggered local dates so a restart in the
    /// same local day does not re-fire a daily alert.
    /// </summary>
    public void RestoreTriggeredDates(IReadOnlyDictionary<AlertType, DateOnly?> dates)
    {
        lock (_gate)
        {
            foreach (var pair in dates)
            {
                if (pair.Key.IsDailyUsageRule())
                {
                    _lastDailyTrigger[pair.Key] = pair.Value;
                }
            }
        }
    }

    private SpeedRuleState EnsureSpeedState(AlertType type)
    {
        if (!_speed.TryGetValue(type, out var state))
        {
            state = new SpeedRuleState();
            _speed[type] = state;
        }

        return state;
    }

    private void EvaluateSpeedRule(AlertType type, double rate, List<AlertSignal> results)
    {
        if (!_config.IsRuleEnabled(type))
        {
            return;
        }

        var threshold = _config.ThresholdOf(type);
        var now = _utcNow();
        var state = EnsureSpeedState(type);

        if (rate < threshold)
        {
            state.Armed = true;
            return;
        }

        if (!state.Armed)
        {
            return;
        }

        state.Armed = false;

        if (now < state.CooldownUntilUtc)
        {
            return;
        }

        state.CooldownUntilUtc = now + _config.Cooldown;
        results.Add(new AlertSignal(type, rate, threshold));
    }

    private void EvaluateDailyRule(
        AlertType type,
        double usage,
        DateOnly todayDate,
        List<AlertSignal> results)
    {
        if (usage < _config.ThresholdOf(type))
        {
            return;
        }

        if (!_config.IsRuleEnabled(type))
        {
            return;
        }

        var last = _lastDailyTrigger.GetValueOrDefault(type);
        if (last == todayDate)
        {
            return;
        }

        _lastDailyTrigger[type] = todayDate;
        results.Add(new AlertSignal(type, usage, _config.ThresholdOf(type)));
    }

    private sealed class SpeedRuleState
    {
        public bool Armed = true;
        public DateTimeOffset CooldownUntilUtc = DateTimeOffset.MinValue;
    }
}