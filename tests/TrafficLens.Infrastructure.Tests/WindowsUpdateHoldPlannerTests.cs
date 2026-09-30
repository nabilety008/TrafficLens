using System.Runtime.Versioning;
using TrafficLens.Core.Abstractions;
using TrafficLens.Infrastructure.Services;

namespace TrafficLens.Infrastructure.Tests;

/// <summary>
/// Pure decision-logic tests for the feature-update hold. No registry access.
/// Snapshots are built with object initializers (init-only properties).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsUpdateHoldPlannerTests
{
    private static readonly WindowsVersionInfo Win11 = new()
    {
        ProductVersion = "Windows 11",
        Release = "24H2",
        BuildNumber = 26100
    };

    private static WindowsUpdateRegistryValue Dword(int value) =>
        new(WindowsUpdatePreviousValueKind.Dword, value, null);

    private static WindowsUpdateRegistryValue Text(string value) =>
        new(WindowsUpdatePreviousValueKind.String, null, value);

    private static WindowsUpdateChangeRecord OwnedHoldRecord() => new()
    {
        SchemaVersion = 2,
        OwnedValues =
        {
            new WindowsUpdateOwnedValue { Name = "ProductVersion", PreviousKind = WindowsUpdatePreviousValueKind.Absent },
            new WindowsUpdateOwnedValue { Name = "TargetReleaseVersion", PreviousKind = WindowsUpdatePreviousValueKind.Absent },
            new WindowsUpdateOwnedValue { Name = "TargetReleaseVersionInfo", PreviousKind = WindowsUpdatePreviousValueKind.Absent }
        }
    };

    private static WindowsUpdateHoldSnapshot DesiredPolicy(
        WindowsUpdateChangeRecord? ownedRecord = null,
        WindowsUpdateRegistryValue? product = null,
        WindowsUpdateRegistryValue? flag = null,
        WindowsUpdateRegistryValue? info = null,
        IReadOnlyList<string>? otherValueNames = null) => new()
    {
        ProductVersionValue = product ?? Text("Windows 11"),
        TargetReleaseVersionValue = flag ?? Dword(1),
        TargetReleaseVersionInfoValue = info ?? Text("24H2"),
        OwnedRecord = ownedRecord,
        OtherValueNames = otherValueNames ?? Array.Empty<string>(),
        VersionDetection = WindowsVersionDetectionResult.Success(Win11)
    };

    // ------------------------------------------------------------ read error

    [Fact]
    public void Resolve_ReadError_ReturnsUnknownWithNoActions()
    {
        var decision = WindowsUpdateHoldPlanner.Resolve(new WindowsUpdateHoldSnapshot
        {
            ReadError = true,
            ErrorMessage = "access denied"
        });

        Assert.Equal(WindowsUpdateStatus.Unknown, decision.Status);
        Assert.Equal("access denied", decision.Error);
        Assert.False(decision.CanEnable);
        Assert.False(decision.CanDisable);
    }

    // -------------------------------------------------------- external policy

    [Fact]
    public void Resolve_ExternalTargetReleaseValue_ReturnsManagedAndBlocksEnable()
    {
        var decision = WindowsUpdateHoldPlanner.Resolve(DesiredPolicy(
            product: Text("Windows 11"), flag: null, info: null));

        Assert.Equal(WindowsUpdateStatus.ManagedByPolicy, decision.Status);
        Assert.Equal(WindowsUpdateDisableReason.Policy, decision.Reason);
        Assert.True(decision.OrganizationPolicyPresent);
        Assert.False(decision.CanEnable);
        Assert.False(decision.CanDisable);
    }

    [Fact]
    public void Resolve_OwnedHoldWithAdditionalExternalValue_StaysEnabled()
    {
        // Unrelated values in the policy key are never treated as blocking.
        var decision = WindowsUpdateHoldPlanner.Resolve(DesiredPolicy(
            ownedRecord: OwnedHoldRecord(),
            otherValueNames: new[] { "SomeOtherPolicyValue" }));

        Assert.Equal(WindowsUpdateStatus.Enabled, decision.Status);
        Assert.Equal(WindowsUpdateDisableReason.TrafficLens, decision.Reason);
        Assert.False(decision.OrganizationPolicyPresent);
    }

    // ------------------------------------------------------- owned hold state

    [Fact]
    public void Resolve_OwnedDesiredHold_ReturnsEnabledWithDetectedTarget()
    {
        var decision = WindowsUpdateHoldPlanner.Resolve(
            DesiredPolicy(ownedRecord: OwnedHoldRecord()));

        Assert.Equal(WindowsUpdateStatus.Enabled, decision.Status);
        Assert.True(decision.CanEnable);   // release the hold
        Assert.Equal("Windows 11 — 24H2", decision.DetectedTarget);
    }

    [Fact]
    public void Resolve_OwnedHoldWithoutPolicyValues_IsIncompleteState()
    {
        var decision = WindowsUpdateHoldPlanner.Resolve(new WindowsUpdateHoldSnapshot
        {
            OwnedRecord = OwnedHoldRecord(),
            VersionDetection = WindowsVersionDetectionResult.Success(Win11)
        });

        Assert.Equal(WindowsUpdateStatus.Unknown, decision.Status);
        Assert.NotNull(decision.Error);
    }

    [Fact]
    public void Resolve_OwnedHoldPartialValues_IsIncompleteState()
    {
        var decision = WindowsUpdateHoldPlanner.Resolve(new WindowsUpdateHoldSnapshot
        {
            OwnedRecord = OwnedHoldRecord(),
            TargetReleaseVersionValue = Dword(1),
            VersionDetection = WindowsVersionDetectionResult.Success(Win11)
        });

        Assert.Equal(WindowsUpdateStatus.Unknown, decision.Status);
        Assert.NotNull(decision.Error);
    }

    [Fact]
    public void Resolve_OwnedHoldOlderRelease_MismatchesDetectedReleaseAndReportsError()
    {
        var decision = WindowsUpdateHoldPlanner.Resolve(DesiredPolicy(
            ownedRecord: OwnedHoldRecord(),
            info: Text("23H2")));

        Assert.Equal(WindowsUpdateStatus.Unknown, decision.Status);
        Assert.Contains("does not match", decision.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_NoOwnershipAndNoPolicy_ReturnsDisabledAvailable()
    {
        var decision = WindowsUpdateHoldPlanner.Resolve(new WindowsUpdateHoldSnapshot
        {
            VersionDetection = WindowsVersionDetectionResult.Success(Win11)
        });

        Assert.Equal(WindowsUpdateStatus.Disabled, decision.Status);
        Assert.Equal(WindowsUpdateDisableReason.None, decision.Reason);
        Assert.True(decision.CanDisable);   // apply the hold
        Assert.False(decision.CanEnable);
        Assert.Equal("Windows 11 — 24H2", decision.DetectedTarget);
    }

    // ------------------------------------------------------------- unsupported

    [Fact]
    public void Resolve_UnknownWindowsRelease_ReturnsDisabledUnsupportedWithNoEnable()
    {
        var decision = WindowsUpdateHoldPlanner.Resolve(new WindowsUpdateHoldSnapshot
        {
            VersionDetection = WindowsVersionDetectionResult.Unsupported(
                WindowsVersionDetectionFailure.UnknownRelease, "release unknown")
        });

        Assert.Equal(WindowsUpdateStatus.Disabled, decision.Status);
        Assert.Equal(WindowsUpdateDisableReason.Unsupported, decision.Reason);
        Assert.False(decision.CanEnable);
        Assert.Null(decision.DetectedTarget);
    }

    // --------------------------------------------------- legacy NoAutoUpdate

    [Fact]
    public void Resolve_LegacyTrafficLensNoAutoUpdate_IsSurfacedNotSilentlyOwned()
    {
        var legacy = new WindowsUpdateChangeRecord
        {
            SchemaVersion = 1,
            PreviousKind = WindowsUpdatePreviousValueKind.Absent,
            PreviousKeyExisted = true
        };
        var decision = WindowsUpdateHoldPlanner.Resolve(new WindowsUpdateHoldSnapshot
        {
            NoAutoUpdateValue = Dword(1),
            LegacyRecord = legacy,
            VersionDetection = WindowsVersionDetectionResult.Success(Win11)
        });

        Assert.Equal(WindowsUpdateStatus.ManagedByPolicy, decision.Status);
        Assert.True(decision.OrganizationPolicyPresent);
        Assert.True(decision.CanDisable);   // applying the hold migrates the legacy policy
        Assert.False(decision.CanEnable);   // nothing of the new hold exists to release
        Assert.True(WindowsUpdateService.LegacyRecordCoversNoAutoUpdate(legacy));
    }

    [Fact]
    public void Resolve_ExternalNoAutoUpdate_ReturnsManagedByPolicyAndBlocksEnable()
    {
        var decision = WindowsUpdateHoldPlanner.Resolve(new WindowsUpdateHoldSnapshot
        {
            NoAutoUpdateValue = Dword(1),   // no TrafficLens record → external
            VersionDetection = WindowsVersionDetectionResult.Success(Win11)
        });

        Assert.Equal(WindowsUpdateStatus.ManagedByPolicy, decision.Status);
        Assert.Equal(WindowsUpdateDisableReason.Policy, decision.Reason);
        Assert.True(decision.OrganizationPolicyPresent);
        Assert.False(decision.CanEnable);
    }

    [Fact]
    public void Resolve_ExternalNoAutoUpdateZero_IsManagedNotError()
    {
        var decision = WindowsUpdateHoldPlanner.Resolve(new WindowsUpdateHoldSnapshot
        {
            NoAutoUpdateValue = Dword(0),
            VersionDetection = WindowsVersionDetectionResult.Success(Win11)
        });

        Assert.Equal(WindowsUpdateStatus.ManagedByPolicy, decision.Status);
        Assert.True(decision.OrganizationPolicyPresent);
    }

    // ------------------------------------------------- desired values / matches

    [Fact]
    public void DesiredValues_ProductVersionFlagAndInfo_OnlyThreeTargetReleaseValues()
    {
        var desired = WindowsUpdateHoldPlanner.DesiredValues(Win11);

        Assert.Equal(3, desired.Count);
        Assert.Equal("ProductVersion", desired[0].Name);
        Assert.Equal("Windows 11", desired[0].DesiredString);
        Assert.Equal("TargetReleaseVersion", desired[1].Name);
        Assert.Equal(1, desired[1].DesiredDword);
        Assert.Equal("TargetReleaseVersionInfo", desired[2].Name);
        Assert.Equal("24H2", desired[2].DesiredString);

        // The new desired state never includes NoAutoUpdate.
        Assert.DoesNotContain(desired, v => v.Name == "NoAutoUpdate");
    }

    [Fact]
    public void MatchesDesired_ExactHold_IsTrue()
    {
        var snapshot = DesiredPolicy(ownedRecord: OwnedHoldRecord());

        Assert.True(WindowsUpdateHoldPlanner.MatchesDesired(snapshot, Win11));
    }

    [Fact]
    public void MatchesDesired_WrongRelease_IsFalse()
    {
        var snapshot = DesiredPolicy(ownedRecord: OwnedHoldRecord(), info: Text("23H2"));

        Assert.False(WindowsUpdateHoldPlanner.MatchesDesired(snapshot, Win11));
    }

    [Fact]
    public void MatchesDesired_NoOwnershipRecord_IsFalseEvenIfValuesMatch()
    {
        var snapshot = DesiredPolicy();

        // Values matching by coincidence (e.g. set externally to the same
        // target) still do not make TrafficLens the owner.
        Assert.False(WindowsUpdateHoldPlanner.MatchesDesired(snapshot, Win11));
    }

    [Fact]
    public void HasUnownedTargetReleaseValue_OwnedNamesAreExcluded()
    {
        var snapshot = DesiredPolicy(ownedRecord: OwnedHoldRecord());

        Assert.False(WindowsUpdateHoldPlanner.HasUnownedTargetReleaseValue(snapshot));
    }

    [Fact]
    public void FormatTarget_UnusableInfo_ReturnsNull()
    {
        Assert.Null(WindowsUpdateHoldPlanner.FormatTarget(new WindowsVersionInfo()));
    }
}
