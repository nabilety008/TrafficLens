namespace TrafficLens.Core.Abstractions;

public sealed class WindowsUpdateState
{
    public WindowsUpdateStatus Status { get; init; }

    public WindowsUpdateDisableReason Reason { get; init; }

    public bool OrganizationPolicyPresent { get; init; }

    public bool CanDisable { get; init; }

    public bool CanEnable { get; init; }

    public string? Error { get; init; }
}
