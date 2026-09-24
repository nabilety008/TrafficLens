namespace TrafficLens.WinUI.ViewModels;

public readonly record struct HistoryChartPoint(
    string Label,
    long DownloadBytes,
    long UploadBytes)
{
    public long TotalBytes => DownloadBytes + UploadBytes;
}
