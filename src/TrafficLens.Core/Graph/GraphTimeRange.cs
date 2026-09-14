namespace TrafficLens.Core.Graph;

public enum GraphTimeRange
{
    ThirtySeconds = 30,
    OneMinute = 60,
    FiveMinutes = 300
}

public static class GraphTimeRangeExtensions
{
    public static TimeSpan ToDuration(this GraphTimeRange range) =>
        TimeSpan.FromSeconds((int)range);

    public static GraphTimeRange FromSeconds(int seconds) => seconds switch
    {
        (int)GraphTimeRange.ThirtySeconds => GraphTimeRange.ThirtySeconds,
        (int)GraphTimeRange.FiveMinutes => GraphTimeRange.FiveMinutes,
        _ => GraphTimeRange.OneMinute
    };
}