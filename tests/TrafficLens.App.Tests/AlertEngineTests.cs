using TrafficLens.Core.Alerts;
using TrafficLens.Core.History;

namespace TrafficLens.App.Tests;

public sealed class AlertEngineTests
{
    private const double OneMb = 1024 * 1024;
    private const double OneGb = OneMb * 1024;

    private sealed class FixedClock
    {
        public DateTimeOffset Now { get; set; } = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        public DateTimeOffset Invoke() => Now;
    }

    private static AlertConfig SpeedConfig(
        bool download = false,
        bool upload = false,
        double downloadThreshold = 50 * OneMb,
        double uploadThreshold = 20 * OneMb,
        TimeSpan? cooldown = null)
    {
        var d = AlertConfig.Default();
        return d with
        {
            HighDownloadSpeedEnabled = download,
            HighUploadSpeedEnabled = upload,
            HighDownloadSpeedThresholdBytesPerSecond = downloadThreshold,
            HighUploadSpeedThresholdBytesPerSecond = uploadThreshold,
            Cooldown = cooldown ?? d.Cooldown
        };
    }

    private static AlertConfig DailyConfig(
        bool download = false,
        bool upload = false,
        bool total = false,
        double downloadLimit = 50 * OneGb,
        double uploadLimit = 20 * OneGb,
        double totalLimit = 100 * OneGb)
    {
        var d = AlertConfig.Default();
        return d with
        {
            DailyDownloadLimitEnabled = download,
            DailyUploadLimitEnabled = upload,
            DailyTotalLimitEnabled = total,
            DailyDownloadLimitBytes = downloadLimit,
            DailyUploadLimitBytes = uploadLimit,
            DailyTotalLimitBytes = totalLimit
        };
    }

    private static HistorySnapshot Today(double download, double upload) => new(
        true,
        null,
        new TrafficUsage((long)download, (long)upload),
        TrafficUsage.Empty,
        TrafficUsage.Empty,
        TrafficUsage.Empty,
        TrafficUsage.Empty,
        Array.Empty<DailyUsagePoint>());

    [Fact]
    public void Speed_BelowThreshold_NoTrigger()
    {
        var engine = new AlertEngine(SpeedConfig(download: true), TimeZoneInfo.Utc, () => DateTimeOffset.UtcNow);

        Assert.Empty(engine.EvaluateSpeed(0, 0));
        Assert.Empty(engine.EvaluateSpeed(49 * OneMb, 0));
        Assert.Empty(engine.EvaluateSpeed(10, 10));
    }

    [Fact]
    public void Speed_UpwardCrossing_TriggersOnce_NoRepeatWhileAbove()
    {
        var engine = new AlertEngine(SpeedConfig(download: true), TimeZoneInfo.Utc, () => DateTimeOffset.UtcNow);

        Assert.Empty(engine.EvaluateSpeed(49 * OneMb, 0));

        var first = engine.EvaluateSpeed(51 * OneMb, 0);
        var signal = Assert.Single(first);
        Assert.Equal(AlertType.HighDownloadSpeed, signal.Type);
        Assert.Equal(51 * OneMb, signal.Value);
        Assert.Equal(50 * OneMb, signal.Threshold);

        Assert.Empty(engine.EvaluateSpeed(60 * OneMb, 0));
        Assert.Empty(engine.EvaluateSpeed(80 * OneMb, 0));
        Assert.Empty(engine.EvaluateSpeed(120 * OneMb, 30 * OneMb));
    }

    [Fact]
    public void Speed_DropBelow_ReArms()
    {
        var engine = new AlertEngine(
            SpeedConfig(download: true, cooldown: TimeSpan.Zero),
            TimeZoneInfo.Utc,
            () => DateTimeOffset.UtcNow);

        Assert.Single(engine.EvaluateSpeed(51 * OneMb, 0));
        Assert.Empty(engine.EvaluateSpeed(10 * OneMb, 0));

        var second = engine.EvaluateSpeed(52 * OneMb, 0);
        Assert.Equal(AlertType.HighDownloadSpeed, Assert.Single(second).Type);
    }

    [Fact]
    public void Speed_RemainsAbove_NoRepeatEvenAfterCooldownExpires()
    {
        var clock = new FixedClock();
        var engine = new AlertEngine(
            SpeedConfig(download: true, cooldown: TimeSpan.FromMinutes(5)),
            TimeZoneInfo.Utc,
            clock.Invoke);

        Assert.Single(engine.EvaluateSpeed(51 * OneMb, 0));

        clock.Now = clock.Now.AddMinutes(10);
        Assert.Empty(engine.EvaluateSpeed(80 * OneMb, 0));

        Assert.Empty(engine.EvaluateSpeed(10 * OneMb, 0));
        Assert.Single(engine.EvaluateSpeed(51 * OneMb, 0));
    }

    [Fact]
    public void Speed_Cooldown_SpacesDistinctCrossings()
    {
        var clock = new FixedClock();
        var engine = new AlertEngine(
            SpeedConfig(download: true, cooldown: TimeSpan.FromMinutes(5)),
            TimeZoneInfo.Utc,
            clock.Invoke);

        Assert.Single(engine.EvaluateSpeed(51 * OneMb, 0));

        clock.Now = clock.Now.AddMinutes(1);
        Assert.Empty(engine.EvaluateSpeed(5 * OneMb, 0));
        Assert.Empty(engine.EvaluateSpeed(52 * OneMb, 0));

        clock.Now = clock.Now.AddMinutes(2);
        Assert.Empty(engine.EvaluateSpeed(5 * OneMb, 0));
        Assert.Empty(engine.EvaluateSpeed(53 * OneMb, 0));

        clock.Now = clock.Now.AddMinutes(4);
        Assert.Empty(engine.EvaluateSpeed(5 * OneMb, 0));
        Assert.Single(engine.EvaluateSpeed(54 * OneMb, 0));
    }

    [Fact]
    public void Speed_DownloadAndUpload_Independent()
    {
        var engine = new AlertEngine(
            SpeedConfig(download: true, upload: true, cooldown: TimeSpan.Zero),
            TimeZoneInfo.Utc,
            () => DateTimeOffset.UtcNow);

        var signals = engine.EvaluateSpeed(60 * OneMb, 5 * OneMb);
        Assert.Single(signals);
        Assert.Equal(AlertType.HighDownloadSpeed, signals[0].Type);

        var uploadOnly = engine.EvaluateSpeed(5 * OneMb, 25 * OneMb);
        var upload = Assert.Single(uploadOnly);
        Assert.Equal(AlertType.HighUploadSpeed, upload.Type);
    }

    [Fact]
    public void Speed_DisabledRule_NeverTriggers()
    {
        var engine = new AlertEngine(SpeedConfig(), TimeZoneInfo.Utc, () => DateTimeOffset.UtcNow);

        Assert.Empty(engine.EvaluateSpeed(900 * OneMb, 900 * OneMb));
        Assert.Empty(engine.EvaluateSpeed(10 * OneMb, 10 * OneMb));
        Assert.Empty(engine.EvaluateSpeed(900 * OneMb, 10 * OneMb));
    }

    [Fact]
    public void Daily_BelowLimit_NoTrigger()
    {
        var engine = new AlertEngine(DailyConfig(download: true), TimeZoneInfo.Utc, () => DateTimeOffset.UtcNow);

        Assert.Empty(engine.EvaluateDaily(Today(40 * OneGb, 0)));
        Assert.Empty(engine.EvaluateDaily(Today(0, 10 * OneGb)));
        Assert.Empty(engine.EvaluateDaily(Today(0, 0)));
    }

    [Fact]
    public void Daily_Crossing_TriggersForEachRule()
    {
        var engine = new AlertEngine(
            DailyConfig(download: true, upload: true, total: true),
            TimeZoneInfo.Utc,
            () => DateTimeOffset.UtcNow);

        var signals = engine.EvaluateDaily(Today(60 * OneGb, 50 * OneGb));
        Assert.Equal(3, signals.Count);
        Assert.Contains(signals, s => s.Type == AlertType.DailyDownloadLimit);
        Assert.Contains(signals, s => s.Type == AlertType.DailyUploadLimit);
        Assert.Contains(signals, s => s.Type == AlertType.DailyTotalLimit);
    }

    [Fact]
    public void Daily_OncePerDay_ThenNextDayReArms()
    {
        var clock = new FixedClock();
        var engine = new AlertEngine(
            DailyConfig(total: true),
            TimeZoneInfo.Utc,
            clock.Invoke);

        var snapshot = Today(60 * OneGb, 60 * OneGb);

        Assert.Single(engine.EvaluateDaily(snapshot));
        Assert.Empty(engine.EvaluateDaily(snapshot));
        Assert.Empty(engine.EvaluateDaily(Today(150 * OneGb, 150 * OneGb)));

        clock.Now = clock.Now.AddDays(1);
        Assert.Single(engine.EvaluateDaily(snapshot));
    }

    [Fact]
    public void Daily_Disabled_NeverTriggers()
    {
        var engine = new AlertEngine(DailyConfig(), TimeZoneInfo.Utc, () => DateTimeOffset.UtcNow);

        Assert.Empty(engine.EvaluateDaily(Today(900 * OneGb, 900 * OneGb)));
    }

    [Fact]
    public void Daily_UnavailableSnapshot_SuspendedSilently()
    {
        var engine = new AlertEngine(
            DailyConfig(total: true),
            TimeZoneInfo.Utc,
            () => DateTimeOffset.UtcNow);

        Assert.Empty(engine.EvaluateDaily(HistorySnapshot.Unavailable("db down")));

        var snapshot = Today(60 * OneGb, 60 * OneGb);
        Assert.Single(engine.EvaluateDaily(snapshot));
    }

    [Fact]
    public void Daily_UsesLocalCalendarDay_NotUtcDate()
    {
        // UTC+14: a UTC evening belongs to the NEXT local calendar day.
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Line Islands Standard Time");
        var clock = new FixedClock { Now = new DateTimeOffset(2026, 6, 1, 20, 0, 0, TimeSpan.Zero) };
        var engine = new AlertEngine(DailyConfig(download: true), tz, clock.Invoke);

        var snapshot = Today(60 * OneGb, 0);
        Assert.Single(engine.EvaluateDaily(snapshot));

        var triggered = engine.TriggeredDailyDates();
        Assert.Equal(new DateOnly(2026, 6, 2), triggered[AlertType.DailyDownloadLimit]);

        // Same local day even after midnight UTC → still one trigger only.
        clock.Now = clock.Now.AddHours(1);
        Assert.Empty(engine.EvaluateDaily(snapshot));

        // Next local day re-arms.
        clock.Now = new DateTimeOffset(2026, 6, 2, 20, 0, 0, TimeSpan.Zero);
        Assert.Single(engine.EvaluateDaily(snapshot));
    }

    [Fact]
    public void RestoreTriggeredDates_PreventsSameDayRepeat()
    {
        var clock = new FixedClock { Now = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero) };
        var engine = new AlertEngine(DailyConfig(total: true), TimeZoneInfo.Utc, clock.Invoke);

        engine.RestoreTriggeredDates(new Dictionary<AlertType, DateOnly?>
        {
            [AlertType.DailyTotalLimit] = new DateOnly(2026, 6, 1)
        });

        Assert.Empty(engine.EvaluateDaily(Today(60 * OneGb, 60 * OneGb)));

        clock.Now = clock.Now.AddDays(1);
        Assert.Single(engine.EvaluateDaily(Today(60 * OneGb, 60 * OneGb)));
    }

    [Fact]
    public void RestoreTriggeredDates_OnlyAcceptsDailyRules()
    {
        var engine = new AlertEngine(DailyConfig(total: true), TimeZoneInfo.Utc, () => DateTimeOffset.UtcNow);

        engine.RestoreTriggeredDates(new Dictionary<AlertType, DateOnly?>
        {
            [AlertType.HighDownloadSpeed] = new DateOnly(2026, 6, 1)
        });

        Assert.Single(engine.EvaluateDaily(Today(60 * OneGb, 60 * OneGb)));
    }

    [Fact]
    public void Config_DefaultsToAllDisabledAndSuggestedThresholds()
    {
        var config = AlertConfig.Default();

        Assert.False(config.IsRuleEnabled(AlertType.HighDownloadSpeed));
        Assert.False(config.IsRuleEnabled(AlertType.HighUploadSpeed));
        Assert.False(config.IsRuleEnabled(AlertType.DailyDownloadLimit));
        Assert.False(config.IsRuleEnabled(AlertType.DailyUploadLimit));
        Assert.False(config.IsRuleEnabled(AlertType.DailyTotalLimit));
        Assert.Equal(50 * OneMb, config.HighDownloadSpeedThresholdBytesPerSecond);
        Assert.Equal(20 * OneMb, config.HighUploadSpeedThresholdBytesPerSecond);
        Assert.Equal(50 * OneGb, config.DailyDownloadLimitBytes);
        Assert.Equal(20 * OneGb, config.DailyUploadLimitBytes);
        Assert.Equal(100 * OneGb, config.DailyTotalLimitBytes);
        Assert.Equal(TimeSpan.FromMinutes(5), config.Cooldown);
    }
}

public sealed class AlertHistoryBufferTests
{
    [Fact]
    public void Add_KeepsNewestFirst()
    {
        var buffer = new AlertHistoryBuffer(100);
        buffer.Add(new AlertEvent(AlertType.HighDownloadSpeed, 1, 2, DateTimeOffset.UtcNow));
        buffer.Add(new AlertEvent(AlertType.DailyTotalLimit, 3, 4, DateTimeOffset.UtcNow));

        var latest = buffer.Latest();
        Assert.Equal(2, latest.Count);
        Assert.Equal(AlertType.DailyTotalLimit, latest[0].Type);
        Assert.Equal(AlertType.HighDownloadSpeed, latest[1].Type);
    }

    [Fact]
    public void Add_TrimsOldestBeyondCapacity()
    {
        var buffer = new AlertHistoryBuffer(3);
        for (var i = 0; i < 5; i++)
        {
            buffer.Add(new AlertEvent(AlertType.HighDownloadSpeed, i, 0, DateTimeOffset.UtcNow));
        }

        var latest = buffer.Latest();
        Assert.Equal(3, buffer.Count);
        Assert.Equal(3, latest.Count);
        Assert.Equal(4, latest[0].Value);
        Assert.Equal(2, latest[2].Value);
    }

    [Fact]
    public void Clear_EmptiesBuffer()
    {
        var buffer = new AlertHistoryBuffer();
        buffer.Add(new AlertEvent(AlertType.HighDownloadSpeed, 1, 2, DateTimeOffset.UtcNow));
        buffer.Clear();

        Assert.Empty(buffer.Latest());
        Assert.Equal(0, buffer.Count);
    }
}