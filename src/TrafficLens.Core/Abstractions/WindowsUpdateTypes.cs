namespace TrafficLens.Core.Abstractions;

/// <summary>
/// Status of the TrafficLens feature-update hold. The hold keeps the machine on
/// its current Windows feature version while security and quality updates
/// continue; it never disables Windows Update.
/// </summary>
public enum WindowsUpdateStatus
{
    /// <summary>The state could not be read.</summary>
    Unknown,

    /// <summary>The feature-update hold is applied by TrafficLens.</summary>
    Enabled,

    /// <summary>No feature-update hold is applied.</summary>
    Disabled,

    /// <summary>A feature-update policy TrafficLens does not own is present.</summary>
    ManagedByPolicy
}

/// <summary>Why the feature-update hold is (or is not) in effect.</summary>
public enum WindowsUpdateDisableReason
{
    None,

    /// <summary>TrafficLens applied the target-release policy.</summary>
    TrafficLens,

    /// <summary>A policy TrafficLens does not own is present.</summary>
    Policy,

    /// <summary>The required Windows version metadata is unavailable.</summary>
    Unsupported
}

public enum WindowsUpdateOperationResult
{
    Success,
    Canceled,
    Failed
}

/// <summary>
/// Resolved UI state of the feature-update hold.
/// </summary>
public sealed class WindowsUpdateState
{
    public WindowsUpdateStatus Status { get; init; }

    public WindowsUpdateDisableReason Reason { get; init; }

    /// <summary>True when a policy TrafficLens does not own exists (owned values may still match).</summary>
    public bool OrganizationPolicyPresent { get; init; }

    public bool CanDisable { get; init; }

    public bool CanEnable { get; init; }

    /// <summary>Detected product/release ("Windows 11 — 24H2") shown in the UI; never a guessed value.</summary>
    public string? DetectedTarget { get; init; }

    public string? Error { get; init; }
}
