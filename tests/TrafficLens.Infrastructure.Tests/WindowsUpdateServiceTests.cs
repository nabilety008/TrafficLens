using TrafficLens.Core.Abstractions;
using System.Runtime.Versioning;
using TrafficLens.Infrastructure.Services;

namespace TrafficLens.Infrastructure.Tests;

/// <summary>
/// Service-level tests for apply/rollback/migration. Every dependency is a
/// fake — no test reads or writes the real HKLM.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsUpdateServiceTests
{
    private const string Product = "ProductVersion";
    private const string Flag = "TargetReleaseVersion";
    private const string Info = "TargetReleaseVersionInfo";

    private static readonly WindowsVersionInfo Win11 = new()
    {
        ProductVersion = "Windows 11",
        Release = "24H2",
        BuildNumber = 26100
    };

    private sealed class FakeStore : IWindowsUpdateRecordStore
    {
        public WindowsUpdateChangeRecord? Record;
        public int Saves;
        public int Deletes;

        public WindowsUpdateChangeRecord? Load() => Record;

        public bool Save(WindowsUpdateChangeRecord record)
        {
            Saves++;
            Record = record;
            return true;
        }

        public void Delete()
        {
            Deletes++;
            Record = null;
        }
    }

    private sealed class FakeDetector : IWindowsVersionDetector
    {
        public WindowsVersionDetectionResult Result { get; set; } =
            WindowsVersionDetectionResult.Success(Win11);

        public WindowsVersionDetectionResult Detect() => Result;
    }

    private sealed class FakeRegistry
    {
        // name → (kind, dword, string). Absent = not present.
        public Dictionary<string, WindowsUpdateRegistryValue> Values { get; } = new();
        public bool KeyExists { get; set; }
        public List<string> Written { get; } = new();
        public List<string> Deleted { get; } = new();

        public WindowsUpdateRegistryValue? Get(string name) =>
            Values.TryGetValue(name, out var value) ? value : null;

        public void SetDword(string name, int value)
        {
            Values[name] = new WindowsUpdateRegistryValue(WindowsUpdatePreviousValueKind.Dword, value, null);
            KeyExists = true;
            Written.Add($"{name}=DWORD:{value}");
        }

        public void SetString(string name, string value)
        {
            Values[name] = new WindowsUpdateRegistryValue(WindowsUpdatePreviousValueKind.String, null, value);
            KeyExists = true;
            Written.Add($"{name}=STR:{value}");
        }

        public void Delete(string name)
        {
            Values.Remove(name);
            Deleted.Add(name);
        }

        /// <summary>
        /// Executes "add \"HKLM\...\" /v NAME /t TYPE /d VALUE /f" segments
        /// separated by " ; " — the exact strings the command builder emits.
        /// </summary>
        public void ApplyRegCommandLine(string arguments)
        {
            foreach (var segment in arguments.Split(" ; "))
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    segment,
                    "^add \"[^\"]+\" /v (?<name>[^ ]+) /t (?<type>REG_DWORD|REG_SZ) /d \"?(?<data>[^\"]*)\"? /f$");
                Assert.True(match.Success, $"Unexpected reg.exe segment: {segment}");

                if (match.Groups["type"].Value == "REG_DWORD")
                {
                    SetDword(match.Groups["name"].Value, int.Parse(match.Groups["data"].Value));
                }
                else
                {
                    SetString(match.Groups["name"].Value, match.Groups["data"].Value);
                }
            }
        }

        /// <summary>Mirrors the cleanup script: removes the key only when empty.</summary>
        public void ApplyEmptyKeyCleanup()
        {
            if (Values.Count == 0)
            {
                KeyExists = false;
            }
        }
    }

    private sealed class Harness
    {
        public FakeRegistry Registry { get; } = new();
        public FakeStore Store { get; } = new();
        public FakeDetector Detector { get; } = new();
        public List<string?> ElevatedCalls { get; } = new();
        public int? ExitCodeToReturn { get; set; } = 0;

        public WindowsUpdateService Service { get; }

        public Harness()
        {
            var reader = new FakeReader(Registry);
            var applier = new FakeApplier(Registry);
            Service = new WindowsUpdateService(
                reader,
                applier,
                Store,
                Detector,
                openAuKey: () => null,
                runElevated: command =>
                {
                    ElevatedCalls.Add(command?.Arguments);
                    if (command is null || ExitCodeToReturn is null)
                    {
                        return ExitCodeToReturn;
                    }

                    // Simulate reg.exe faithfully so the post-write verification
                    // sees the exact state the real command would produce.
                    if (command.FileName == "reg.exe" && ExitCodeToReturn == 0)
                    {
                        Registry.ApplyRegCommandLine(command.Arguments);
                    }
                    else if (command.FileName == "powershell.exe" && ExitCodeToReturn == 0)
                    {
                        Registry.ApplyEmptyKeyCleanup();
                    }

                    return ExitCodeToReturn;
                });
        }

        private sealed class FakeReader : IWindowsUpdatePolicyReader
        {
            private readonly FakeRegistry _registry;

            public FakeReader(FakeRegistry registry) => _registry = registry;

            public bool KeyExists => _registry.KeyExists;

            public IReadOnlyList<string> ValueNames =>
                _registry.Values.Keys.ToArray();

            public WindowsUpdateRegistryValue? GetValue(string name) => _registry.Get(name);
        }

        private sealed class FakeApplier : IWindowsUpdatePolicyApplier
        {
            private readonly FakeRegistry _registry;

            public FakeApplier(FakeRegistry registry) => _registry = registry;

            public bool WriteDword(string name, int value)
            {
                _registry.SetDword(name, value);
                return true;
            }

            public bool WriteString(string name, string value)
            {
                _registry.SetString(name, value);
                return true;
            }

            public bool DeleteValue(string name)
            {
                _registry.Delete(name);
                return true;
            }
        }
    }

    // ------------------------------------------------------------------ apply

    [Fact]
    public async Task Apply_CleanMachine_WritesExactTargetReleasePolicy()
    {
        var h = new Harness();

        var result = await h.Service.DisableAsync();

        Assert.Equal(WindowsUpdateOperationResult.Success, result);
        Assert.Equal("Windows 11", h.Registry.Get(Product)!.Text);
        Assert.Equal(1, h.Registry.Get(Flag)!.Dword);
        Assert.Equal("24H2", h.Registry.Get(Info)!.Text);
        Assert.Equal(3, h.Registry.Values.Count);

        // The new implementation never writes NoAutoUpdate.
        Assert.Null(h.Registry.Get("NoAutoUpdate"));
        Assert.DoesNotContain(h.Registry.Written, w => w.StartsWith("NoAutoUpdate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Apply_SavesOwnershipRecordWithPreviousAbsentState()
    {
        var h = new Harness();

        await h.Service.DisableAsync();

        var record = h.Store.Record;
        Assert.NotNull(record);
        Assert.Equal(2, record!.SchemaVersion);
        Assert.Equal(3, record.OwnedValues.Count);
        Assert.All(record.OwnedValues, v =>
            Assert.Equal(WindowsUpdatePreviousValueKind.Absent, v.PreviousKind));
        Assert.False(record.PreviousKeyExisted);
    }

    [Fact]
    public async Task Apply_IsIdempotent_SecondRunWritesNothingNew()
    {
        var h = new Harness();
        await h.Service.DisableAsync();
        var writesAfterFirst = h.Registry.Written.Count;

        var result = await h.Service.DisableAsync();

        Assert.Equal(WindowsUpdateOperationResult.Success, result);
        Assert.Equal(writesAfterFirst, h.Registry.Written.Count);
    }

    [Fact]
    public async Task Apply_PreExistingUnownedValues_AreNeverOverwritten()
    {
        var h = new Harness();
        // A target-release policy set externally (no TrafficLens record).
        h.Registry.SetString(Product, "Windows 10");
        h.Registry.SetDword(Flag, 1);
        h.Registry.SetString(Info, "22H2");

        var result = await h.Service.DisableAsync();

        Assert.Equal(WindowsUpdateOperationResult.Failed, result);
        Assert.Null(h.Store.Record);
        Assert.Equal("Windows 10", h.Registry.Get(Product)!.Text);
        Assert.Equal("22H2", h.Registry.Get(Info)!.Text);
    }

    [Fact]
    public async Task Apply_PreOwnedValuesWhenReEnabling_SnapshotsTheirPreviousState()
    {
        var h = new Harness();
        await h.Service.DisableAsync();
        // Simulate a release change: the owned values now hold an older target.
        h.Registry.SetString(Product, "Windows 11");
        h.Registry.SetDword(Flag, 1);
        h.Registry.SetString(Info, "23H2");

        var result = await h.Service.DisableAsync();

        Assert.Equal(WindowsUpdateOperationResult.Success, result);
        var record = h.Store.Record!;
        var prod = record.OwnedValues.Single(v => v.Name == Product);
        Assert.Equal(WindowsUpdatePreviousValueKind.String, prod.PreviousKind);
        Assert.Equal("Windows 11", prod.PreviousString);   // unchanged previous state kept
        var info = record.OwnedValues.Single(v => v.Name == Info);
        Assert.Equal("23H2", info.PreviousString);
    }

    [Fact]
    public async Task Apply_ExternalUnownedTargetPolicy_FailsAndWritesNothing()
    {
        var h = new Harness();
        h.Registry.SetString(Product, "Windows 11");   // set externally, no record

        var result = await h.Service.DisableAsync();

        Assert.Equal(WindowsUpdateOperationResult.Failed, result);
        Assert.Equal("Windows 11", h.Registry.Get(Product)!.Text);
        Assert.Null(h.Registry.Get(Flag));      // nothing else was written
        Assert.Null(h.Registry.Get(Info));
        Assert.Null(h.Store.Record);
    }

    [Fact]
    public async Task Apply_UnknownWindowsRelease_FailsWithoutWriting()
    {
        var h = new Harness();
        h.Detector.Result = WindowsVersionDetectionResult.Unsupported(
            WindowsVersionDetectionFailure.UnknownRelease, "no release metadata");

        var result = await h.Service.DisableAsync();

        Assert.Equal(WindowsUpdateOperationResult.Failed, result);
        Assert.Empty(h.Registry.Values);
        Assert.Null(h.Store.Record);
    }

    [Fact]
    public async Task Apply_PartialWriteFailure_RollsBackOwnedValuesAndFails()
    {
        var h = new Harness { ExitCodeToReturn = 1 };   // reg.exe reports failure

        var result = await h.Service.DisableAsync();

        Assert.Equal(WindowsUpdateOperationResult.Failed, result);

        // The best-effort rollback removed exactly what the apply wrote — the
        // machine ends in its pre-apply state.
        Assert.Null(h.Registry.Get(Product));
        Assert.Null(h.Registry.Get(Flag));
        Assert.Null(h.Registry.Get(Info));
    }

    [Fact]
    public async Task Apply_ElevationCanceled_ReturnsCanceledAndRollsBack()
    {
        var h = new Harness { ExitCodeToReturn = null };   // UAC declined

        var result = await h.Service.DisableAsync();

        Assert.Equal(WindowsUpdateOperationResult.Canceled, result);
        Assert.Null(h.Registry.Get(Product));
        Assert.Null(h.Registry.Get(Flag));
        Assert.Null(h.Registry.Get(Info));
    }

    [Fact]
    public async Task Apply_LegacyTrafficLensNoAutoUpdate_MigratesOwnershipAndRestoresPreviousState()
    {
        var h = new Harness();
        // Old TrafficLens set NoAutoUpdate=1; the record proves ownership and
        // stores the exact pre-TrafficLens state (a DWORD 0 that existed before).
        h.Registry.SetDword("NoAutoUpdate", 1);
        h.Store.Record = new WindowsUpdateChangeRecord
        {
            SchemaVersion = 1,
            PreviousKind = WindowsUpdatePreviousValueKind.Dword,
            PreviousDword = 0,
            PreviousKeyExisted = true
        };

        var result = await h.Service.DisableAsync();

        Assert.Equal(WindowsUpdateOperationResult.Success, result);

        // New desired state present, NoAutoUpdate no longer written by TrafficLens.
        Assert.Equal("24H2", h.Registry.Get(Info)!.Text);
        Assert.Equal(1, h.Registry.Get("NoAutoUpdate")!.Dword);   // legacy value still there…

        // …but the merged record now owns it with the exact original state,
        // so a later rollback restores NoAutoUpdate=0.
        var record = h.Store.Record!;
        Assert.Equal(2, record.SchemaVersion);
        var noAuto = record.OwnedValues.Single(v => v.Name == "NoAutoUpdate");
        Assert.Equal(WindowsUpdatePreviousValueKind.Dword, noAuto.PreviousKind);
        Assert.Equal(0, noAuto.PreviousDword);
    }

    [Fact]
    public async Task Apply_LegacyRecordAbsentBefore_RollbackDeletesNoAutoUpdate()
    {
        var h = new Harness();
        h.Registry.SetDword("NoAutoUpdate", 1);
        h.Store.Record = new WindowsUpdateChangeRecord
        {
            SchemaVersion = 1,
            PreviousKind = WindowsUpdatePreviousValueKind.Absent,   // did not exist before TrafficLens
            PreviousKeyExisted = true
        };

        await h.Service.DisableAsync();
        await h.Service.EnableAsync();   // rollback

        Assert.Null(h.Registry.Get("NoAutoUpdate"));
        Assert.Null(h.Registry.Get(Info));
        Assert.Null(h.Store.Record);
    }

    // --------------------------------------------------------------- rollback

    [Fact]
    public async Task Rollback_CleanMachine_IsIdempotentNoOpSuccess()
    {
        var h = new Harness();

        var result = await h.Service.EnableAsync();

        Assert.Equal(WindowsUpdateOperationResult.Success, result);
        Assert.Empty(h.Registry.Deleted);
    }

    [Fact]
    public async Task Rollback_RemovesOnlyTrafficLensCreatedValues()
    {
        var h = new Harness();
        h.Registry.SetString("SomeExternalPolicy", "1");   // unrelated value
        await h.Service.DisableAsync();
        h.Registry.Written.Clear();

        var result = await h.Service.EnableAsync();

        Assert.Equal(WindowsUpdateOperationResult.Success, result);
        Assert.Null(h.Registry.Get(Product));
        Assert.Null(h.Registry.Get(Flag));
        Assert.Null(h.Registry.Get(Info));

        // Unrelated values and their snapshot state are untouched.
        Assert.Equal("1", h.Registry.Get("SomeExternalPolicy")!.Text);
        Assert.DoesNotContain(h.Registry.Deleted, d => d == "SomeExternalPolicy");
        Assert.Null(h.Store.Record);
    }

    [Fact]
    public async Task Rollback_RestoresPreExistingValuesExactly()
    {
        var h = new Harness();
        h.Registry.SetString(Product, "Windows 10");
        h.Registry.SetDword(Flag, 0);
        h.Registry.SetString(Info, "22H2");

        await h.Service.DisableAsync();
        var result = await h.Service.EnableAsync();

        Assert.Equal(WindowsUpdateOperationResult.Success, result);
        Assert.Equal("Windows 10", h.Registry.Get(Product)!.Text);
        Assert.Equal(0, h.Registry.Get(Flag)!.Dword);
        Assert.Equal("22H2", h.Registry.Get(Info)!.Text);
        Assert.Null(h.Store.Record);
    }

    [Fact]
    public async Task Rollback_RepeatedRollback_IsSafe()
    {
        var h = new Harness();
        await h.Service.DisableAsync();

        var first = await h.Service.EnableAsync();
        var second = await h.Service.EnableAsync();

        Assert.Equal(WindowsUpdateOperationResult.Success, first);
        Assert.Equal(WindowsUpdateOperationResult.Success, second);
        Assert.Empty(h.Registry.Values);
    }

    [Fact]
    public async Task Rollback_WithMigratedLegacyNoAutoUpdate_RestoresOriginalValue()
    {
        var h = new Harness();
        h.Registry.SetDword("NoAutoUpdate", 1);
        h.Store.Record = new WindowsUpdateChangeRecord
        {
            SchemaVersion = 1,
            PreviousKind = WindowsUpdatePreviousValueKind.Dword,
            PreviousDword = 0,
            PreviousKeyExisted = true
        };

        await h.Service.DisableAsync();     // hold applied, legacy ownership merged
        var result = await h.Service.EnableAsync();

        Assert.Equal(WindowsUpdateOperationResult.Success, result);
        Assert.Equal(0, h.Registry.Get("NoAutoUpdate")!.Dword);   // exact original state
        Assert.Null(h.Registry.Get(Product));
        Assert.Null(h.Registry.Get(Flag));
        Assert.Null(h.Registry.Get(Info));
    }

    [Fact]
    public async Task Rollback_ExternalNoAutoUpdate_IsNotTouched()
    {
        var h = new Harness();
        h.Registry.SetDword("NoAutoUpdate", 1);   // external: no record proves ownership

        var result = await h.Service.EnableAsync();

        // Nothing owned → idempotent success, and the external policy survives.
        Assert.Equal(WindowsUpdateOperationResult.Success, result);
        Assert.Equal(1, h.Registry.Get("NoAutoUpdate")!.Dword);
    }

    [Fact]
    public async Task Rollback_DoesNotRunElevatedCleanupWhenKeyPreExisted()
    {
        var h = new Harness();
        h.Registry.KeyExists = true;
        await h.Service.DisableAsync();
        h.ElevatedCalls.Clear();

        await h.Service.EnableAsync();

        Assert.DoesNotContain(h.ElevatedCalls, c => c is not null && c.Contains("Remove-Item"));
    }

    // ------------------------------------------------------------------- state

    [Fact]
    public async Task GetState_EnabledHold_ReportsEnabledWithTarget()
    {
        var h = new Harness();
        await h.Service.DisableAsync();

        var state = h.Service.GetState();

        Assert.Equal(WindowsUpdateStatus.Enabled, state.Status);
        Assert.Equal(WindowsUpdateDisableReason.TrafficLens, state.Reason);
        Assert.True(state.CanEnable);
        Assert.False(state.CanDisable);
        Assert.Equal("Windows 11 — 24H2", state.DetectedTarget);
    }

    [Fact]
    public void GetState_ExternalManagedPolicy_IsNotReportedAsOwned()
    {
        var h = new Harness();
        h.Registry.SetString(Product, "Windows 11");   // external target-release policy

        var state = h.Service.GetState();

        Assert.Equal(WindowsUpdateStatus.ManagedByPolicy, state.Status);
        Assert.True(state.OrganizationPolicyPresent);
        Assert.False(state.CanEnable);
        Assert.False(state.CanDisable);
    }

    [Fact]
    public void GetState_UnsupportedRelease_ReportsUnavailable()
    {
        var h = new Harness();
        h.Detector.Result = WindowsVersionDetectionResult.Unsupported(
            WindowsVersionDetectionFailure.UnknownRelease, "release unknown");

        var state = h.Service.GetState();

        Assert.Equal(WindowsUpdateStatus.Disabled, state.Status);
        Assert.Equal(WindowsUpdateDisableReason.Unsupported, state.Reason);
        Assert.False(state.CanEnable);
        Assert.False(state.CanDisable);
        Assert.Null(state.DetectedTarget);
    }

    [Fact]
    public void GetState_ReadError_ReportsUnknownWithError()
    {
        var h = new Harness();
        // Simulate a policy reader that throws: replace with a throwing reader.
        var reader = new ThrowingReader();
        var service = new WindowsUpdateService(
            reader,
            new ThrowingApplier(),
            h.Store,
            h.Detector,
            openAuKey: () => null,
            runElevated: _ => 0);

        var state = service.GetState();

        Assert.Equal(WindowsUpdateStatus.Unknown, state.Status);
        Assert.NotNull(state.Error);
        Assert.False(state.CanEnable);
        Assert.False(state.CanDisable);
    }

    [Fact]
    public async Task GetState_ErrorStateAfterPartialFailure_IsSurfacedAsIncomplete()
    {
        var h = new Harness { ExitCodeToReturn = 1 };   // apply fails, rollback runs
        await h.Service.DisableAsync();

        var state = h.Service.GetState();

        // The rollback removed everything; no ownership record remains, so the
        // machine is back to "available", not stuck in a half-owned state.
        Assert.Equal(WindowsUpdateStatus.Disabled, state.Status);
        Assert.True(state.CanDisable);
        Assert.Null(h.Store.Record);
    }

    private sealed class ThrowingReader : IWindowsUpdatePolicyReader
    {
        public bool KeyExists => throw new InvalidOperationException("registry unavailable");

        public IReadOnlyList<string> ValueNames => throw new InvalidOperationException("registry unavailable");

        public WindowsUpdateRegistryValue? GetValue(string name) =>
            throw new InvalidOperationException("registry unavailable");
    }

    private sealed class ThrowingApplier : IWindowsUpdatePolicyApplier
    {
        public bool WriteDword(string name, int value) => throw new InvalidOperationException();

        public bool WriteString(string name, string value) => throw new InvalidOperationException();

        public bool DeleteValue(string name) => throw new InvalidOperationException();
    }
}
