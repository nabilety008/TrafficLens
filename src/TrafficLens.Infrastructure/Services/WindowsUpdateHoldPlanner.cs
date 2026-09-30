namespace TrafficLens.Infrastructure.Services;

using TrafficLens.Core.Abstractions;

/// <summary>
/// Immutable observation of everything the feature-update hold depends on:
/// the current target-release policy values, other policy values, the legacy
/// NoAutoUpdate state and the TrafficLens ownership record.
/// </summary>
public sealed class WindowsUpdateHoldSnapshot
{
    public bool ReadError { get; init; }

    public string? ErrorMessage { get; init; }

    // --- target-release values (the only values TrafficLens now writes)

    public WindowsUpdateRegistryValue? ProductVersionValue { get; init; }

    public WindowsUpdateRegistryValue? TargetReleaseVersionValue { get; init; }

    public WindowsUpdateRegistryValue? TargetReleaseVersionInfoValue { get; init; }

    // --- everything else in the policy key (never touched)

    public IReadOnlyList<string> OtherValueNames { get; init; } = Array.Empty<string>();

    public bool KeyExists { get; init; }

    // --- legacy NoAutoUpdate state (read for migration only, never written)

    public WindowsUpdateRegistryValue? NoAutoUpdateValue { get; init; }

    /// <summary>Legacy schema-1 ownership record (old NoAutoUpdate behavior).</summary>
    public WindowsUpdateChangeRecord? LegacyRecord { get; init; }

    /// <summary>Current schema-2 ownership record for the target-release hold.</summary>
    public WindowsUpdateChangeRecord? OwnedRecord { get; init; }

    public WindowsVersionDetectionResult VersionDetection { get; init; } =
        WindowsVersionDetectionResult.Unsupported(WindowsVersionDetectionFailure.Unreadable, "not detected");
}

/// <summary>What the resolver decided the UI should show and allow.</summary>
public sealed class WindowsUpdateHoldDecision
{
    public WindowsUpdateStatus Status { get; init; }

    public WindowsUpdateDisableReason Reason { get; init; }

    public bool OrganizationPolicyPresent { get; init; }

    public bool CanDisable { get; init; }

    public bool CanEnable { get; init; }

    public string? DetectedTarget { get; init; }

    public string? Error { get; init; }
}

/// <summary>
/// Pure decision logic for the feature-update hold. No registry, no process —
/// fully unit-testable.
/// </summary>
public static class WindowsUpdateHoldPlanner
{
    public static readonly string ProductVersionName = "ProductVersion";
    public static readonly string TargetReleaseVersionName = "TargetReleaseVersion";
    public static readonly string TargetReleaseVersionInfoName = "TargetReleaseVersionInfo";

    /// <summary>The complete set of values TrafficLens may ever write.</summary>
    public static readonly IReadOnlyList<string> OwnedValueNames = new[]
    {
        ProductVersionName,
        TargetReleaseVersionName,
        TargetReleaseVersionInfoName
    };

    /// <summary>
    /// True when the three target-release values hold exactly the desired
    /// state for the detected release.
    /// </summary>
    public static bool MatchesDesired(WindowsUpdateHoldSnapshot snapshot, WindowsVersionInfo target)
    {
        var product = snapshot.ProductVersionValue;
        var flag = snapshot.TargetReleaseVersionValue;
        var info = snapshot.TargetReleaseVersionInfoValue;

        return product is { Kind: WindowsUpdatePreviousValueKind.String }
            && string.Equals(product.Text, target.ProductVersion, StringComparison.OrdinalIgnoreCase)
            && flag is { Kind: WindowsUpdatePreviousValueKind.Dword, Dword: 1 }
            && info is { Kind: WindowsUpdatePreviousValueKind.String }
            && string.Equals(info.Text, target.Release, StringComparison.OrdinalIgnoreCase)
            && OwnedRecordCoversAll(snapshot);
    }

    /// <summary>
    /// True when an ownership record exists that covers all three target-release
    /// values (TrafficLens owns the current hold).
    /// </summary>
    public static bool OwnedRecordCoversAll(WindowsUpdateHoldSnapshot snapshot) =>
        snapshot.OwnedRecord is { } record
        && OwnedValueNames.All(name =>
            record.OwnedValues.Any(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// True when at least one target-release value exists that TrafficLens does
    /// not own — an external/managed policy that must never be overwritten.
    /// </summary>
    public static bool HasUnownedTargetReleaseValue(WindowsUpdateHoldSnapshot snapshot)
    {
        var ownedNames = snapshot.OwnedRecord is { } record
            ? record.OwnedValues.Select(v => v.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return TargetReleaseNames()
            .Any(name => snapshot.HasValue(name) && !ownedNames.Contains(name));
    }

    private static IEnumerable<string> TargetReleaseNames() => OwnedValueNames;

    internal static bool HasValue(this WindowsUpdateHoldSnapshot snapshot, string name) => name switch
    {
        var n when n == ProductVersionName => snapshot.ProductVersionValue is not null,
        var n when n == TargetReleaseVersionName => snapshot.TargetReleaseVersionValue is not null,
        var n when n == TargetReleaseVersionInfoName => snapshot.TargetReleaseVersionInfoValue is not null,
        _ => false
    };

    /// <summary>
    /// Resolves the UI state from a snapshot.
    /// Precedence: read error → external target-release policy (managed) →
    /// owned hold (enabled) → legacy TrafficLens NoAutoUpdate hold (surfaced as
    /// external policy needing migration; never silently deleted) →
    /// version unsupported → not held (available).
    /// </summary>
    public static WindowsUpdateHoldDecision Resolve(WindowsUpdateHoldSnapshot snapshot)
    {
        if (snapshot.ReadError)
        {
            return new WindowsUpdateHoldDecision
            {
                Status = WindowsUpdateStatus.Unknown,
                Error = string.IsNullOrWhiteSpace(snapshot.ErrorMessage)
                    ? "The Windows Update policy state could not be read."
                    : snapshot.ErrorMessage
            };
        }

        if (HasUnownedTargetReleaseValue(snapshot))
        {
            return new WindowsUpdateHoldDecision
            {
                Status = WindowsUpdateStatus.ManagedByPolicy,
                Reason = WindowsUpdateDisableReason.Policy,
                OrganizationPolicyPresent = true
            };
        }

        if (snapshot.OwnedRecord is not null && OwnedRecordCoversAll(snapshot)
            && snapshot.VersionDetection is { IsUsable: true } detection
            && MatchesDesired(snapshot, detection.Info))
        {
            return new WindowsUpdateHoldDecision
            {
                Status = WindowsUpdateStatus.Enabled,
                Reason = WindowsUpdateDisableReason.TrafficLens,
                CanEnable = true,
                DetectedTarget = FormatTarget(detection.Info)
            };
        }

        if (snapshot.OwnedRecord is not null && OwnedRecordCoversAll(snapshot))
        {
            // TrafficLens owns the values but they no longer match the detected
            // release (OS upgraded across releases) — the hold is incomplete.
            return new WindowsUpdateHoldDecision
            {
                Status = WindowsUpdateStatus.Unknown,
                Reason = WindowsUpdateDisableReason.TrafficLens,
                CanEnable = true,
                DetectedTarget = snapshot.VersionDetection.IsUsable
                    ? FormatTarget(snapshot.VersionDetection.Info)
                    : null,
                Error = "The feature-update hold does not match the current Windows release."
            };
        }

        if (snapshot.OwnedRecord is not null)
        {
            // A partial/foreign combination of owned values — treat as
            // incomplete state that only rollback can clear.
            return new WindowsUpdateHoldDecision
            {
                Status = WindowsUpdateStatus.Unknown,
                Reason = WindowsUpdateDisableReason.TrafficLens,
                CanEnable = true,
                Error = "The feature-update hold state is incomplete."
            };
        }

        // No TrafficLens ownership: external policies in other values do not
        // block a target-release hold UNLESS they set target-release values,
        // which was handled above. Legacy NoAutoUpdate written by old TrafficLens
        // is surfaced as a migration case: the offered action is the hold, whose
        // apply carries the legacy ownership into the new record and restores
        // NoAutoUpdate's original state. There is nothing to "release" — the
        // legacy hold is not owned by the new record.
        if (snapshot.NoAutoUpdateValue is not null && snapshot.LegacyRecord is not null)
        {
            return new WindowsUpdateHoldDecision
            {
                Status = WindowsUpdateStatus.ManagedByPolicy,
                Reason = WindowsUpdateDisableReason.TrafficLens,
                OrganizationPolicyPresent = true,
                CanDisable = snapshot.VersionDetection.IsUsable,
                DetectedTarget = snapshot.VersionDetection.IsUsable
                    ? FormatTarget(snapshot.VersionDetection.Info)
                    : null,
                Error = "A legacy TrafficLens Windows Update policy is present and will be restored when the feature-update hold is applied."
            };
        }

        // Any NoAutoUpdate value without a TrafficLens ownership record is
        // external (organization or unknown origin) and is surfaced as managed
        // regardless of its value — TrafficLens never overwrites or deletes it.
        if (snapshot.NoAutoUpdateValue is not null)
        {
            return new WindowsUpdateHoldDecision
            {
                Status = WindowsUpdateStatus.ManagedByPolicy,
                Reason = WindowsUpdateDisableReason.Policy,
                OrganizationPolicyPresent = true
            };
        }

        if (!snapshot.VersionDetection.IsUsable)
        {
            return new WindowsUpdateHoldDecision
            {
                Status = WindowsUpdateStatus.Disabled,
                Reason = WindowsUpdateDisableReason.Unsupported,
                DetectedTarget = null,
                Error = snapshot.VersionDetection.Error
            };
        }

        // Not held: the offered action is applying the hold. (Naming follows
        // the historical API: DisableAsync applies the hold, EnableAsync —
        // shown as "Release" — removes it.)
        return new WindowsUpdateHoldDecision
        {
            Status = WindowsUpdateStatus.Disabled,
            Reason = WindowsUpdateDisableReason.None,
            CanDisable = true,
            DetectedTarget = FormatTarget(snapshot.VersionDetection.Info)
        };
    }

    public static string? FormatTarget(WindowsVersionInfo info) =>
        info.IsUsable ? $"{info.ProductVersion} — {info.Release}" : null;

    /// <summary>
    /// The desired values for the target-release hold, per Microsoft's
    /// supported "Select the target Feature Update version" policy:
    /// ProductVersion = "Windows 11"/"Windows 10" (REG_SZ),
    /// TargetReleaseVersion = 1 (REG_DWORD enabling flag),
    /// TargetReleaseVersionInfo = "24H2" etc. (REG_SZ).
    /// </summary>
    public static IReadOnlyList<DesiredValue> DesiredValues(WindowsVersionInfo target) => new[]
    {
        new DesiredValue(ProductVersionName, WindowsUpdatePreviousValueKind.String, null, target.ProductVersion!),
        new DesiredValue(TargetReleaseVersionName, WindowsUpdatePreviousValueKind.Dword, 1, null),
        new DesiredValue(TargetReleaseVersionInfoName, WindowsUpdatePreviousValueKind.String, null, target.Release!)
    };

    public sealed record DesiredValue(
        string Name,
        WindowsUpdatePreviousValueKind DesiredKind,
        int? DesiredDword,
        string? DesiredString);
}
