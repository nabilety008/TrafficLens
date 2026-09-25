namespace TrafficLens.Infrastructure.Services;

public enum WindowsUpdatePreviousValueKind
{
    Absent,
    Dword,
    String
}

public sealed class WindowsUpdateChangeRecord
{
    public WindowsUpdatePreviousValueKind PreviousKind { get; set; } = WindowsUpdatePreviousValueKind.Absent;

    public int? PreviousDword { get; set; }

    public string? PreviousString { get; set; }

    public DateTime ChangedAtUtc { get; set; }
}
