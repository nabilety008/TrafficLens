namespace TrafficLens.WinUI.ViewModels;

/// <summary>
/// One already-loaded history chart entry. <see cref="FullPeriodLabel"/> is the
/// unabbreviated period (e.g. "2026-09-27" or "14:00–15:00") used by hover,
/// while <see cref="Label"/> stays the compact axis label.
/// </summary>
public readonly record struct HistoryChartPoint(
    string Label,
    long DownloadBytes,
    long UploadBytes,
    string FullPeriodLabel = "")
{
    public long TotalBytes => DownloadBytes + UploadBytes;
}
