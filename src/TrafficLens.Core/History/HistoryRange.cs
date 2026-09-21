namespace TrafficLens.Core.History;

/// <summary>
/// Reporting ranges for the History page. All ranges are expressed in the user's
/// LOCAL calendar (see <see cref="HistoryRangeCalculator"/>); persisted data is
/// always UTC.
/// </summary>
public enum HistoryRange
{
    Today,
    Yesterday,
    Last7Days,
    Last30Days,
    Lifetime,
    ThisMonth
}
