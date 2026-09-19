namespace TrafficLens.App.ViewModels;

/// <summary>
/// One ready-to-render chart bar. The ViewModel precomputes the axis label
/// (local date for daily ranges, local hour for the Today hourly series) so the
/// chart control stays a dumb presenter and never formats history data.
/// Label is a technical value and is always rendered LTR.
/// </summary>
public readonly record struct HistoryChartPoint(
    string Label,
    long DownloadBytes,
    long UploadBytes)
{
    public long TotalBytes => DownloadBytes + UploadBytes;
}