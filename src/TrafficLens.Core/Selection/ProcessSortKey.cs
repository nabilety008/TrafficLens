namespace TrafficLens.Core.Selection;

/// <summary>
/// Sort keys for the Applications list. Sorting operates purely on a snapshot
/// (a copy of the collector's read-only sample list) and never mutates
/// collector accounting state.
/// </summary>
public enum ProcessSortKey
{
    TotalRate,
    DownloadRate,
    UploadRate,
    TotalTransferred,
    Downloaded,
    Uploaded,
    Name
}