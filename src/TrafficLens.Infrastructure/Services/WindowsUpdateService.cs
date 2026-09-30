using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;
using TrafficLens.Core.Abstractions;

namespace TrafficLens.Infrastructure.Services;

/// <summary>
/// Implements the TrafficLens feature-update hold: Windows stays on its CURRENT
/// feature version via Microsoft's supported target-release policy
/// (ProductVersion / TargetReleaseVersion / TargetReleaseVersionInfo) while
/// security and quality updates continue. This service never disables Windows
/// Update: it does not write NoAutoUpdate, does not pause or defer quality
/// updates, and never touches update services, BITS or Defender.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsUpdateService : IWindowsUpdateService
{
    private const string AuKeyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
    private const string NoAutoUpdateName = "NoAutoUpdate";

    private readonly IWindowsUpdatePolicyReader _reader;
    private readonly IWindowsUpdatePolicyApplier _applier;
    private readonly IWindowsUpdateRecordStore _recordStore;
    private readonly IWindowsVersionDetector _versionDetector;
    private readonly Func<RegistryKey?> _openAuKey;
    private readonly Func<WindowsUpdateRegistryCommand?, int?> _runElevated;

    public WindowsUpdateService()
        : this(
            new WindowsUpdatePolicyRegistry(),
            new WindowsUpdatePolicyRegistry(),
            new WindowsUpdateRecordStore(Path.Combine(AppPaths.RootDirectory, "windowsupdate-state.json")),
            new WindowsVersionDetector())
    {
    }

    internal WindowsUpdateService(
        IWindowsUpdatePolicyReader reader,
        IWindowsUpdatePolicyApplier applier,
        IWindowsUpdateRecordStore recordStore,
        IWindowsVersionDetector versionDetector,
        Func<RegistryKey?>? openAuKey = null,
        Func<WindowsUpdateRegistryCommand?, int?>? runElevated = null)
    {
        _reader = reader;
        _applier = applier;
        _recordStore = recordStore;
        _versionDetector = versionDetector;
        _openAuKey = openAuKey ?? DefaultOpenAuKey;
        _runElevated = runElevated ?? RunElevatedProcess;
    }

    public WindowsUpdateState GetState()
    {
        var snapshot = ReadSnapshot();
        var decision = WindowsUpdateHoldPlanner.Resolve(snapshot);
        return new WindowsUpdateState
        {
            Status = decision.Status,
            Reason = decision.Reason,
            OrganizationPolicyPresent = decision.OrganizationPolicyPresent,
            CanDisable = decision.CanDisable,
            CanEnable = decision.CanEnable,
            DetectedTarget = decision.DetectedTarget,
            Error = decision.Error
        };
    }

    public Task<WindowsUpdateOperationResult> DisableAsync(CancellationToken cancellationToken = default) =>
        Task.Run(ApplyHoldCore, cancellationToken);

    public Task<WindowsUpdateOperationResult> EnableAsync(CancellationToken cancellationToken = default) =>
        Task.Run(RollbackCore, cancellationToken);

    // ------------------------------------------------------------------ apply

    private WindowsUpdateOperationResult ApplyHoldCore()
    {
        var snapshot = ReadSnapshot();
        if (snapshot.ReadError)
        {
            return WindowsUpdateOperationResult.Failed;
        }

        var detection = snapshot.VersionDetection;
        if (!detection.IsUsable)
        {
            // Never guess and never write an incomplete policy.
            return WindowsUpdateOperationResult.Failed;
        }

        // An unowned target-release policy (organization-managed or externally
        // configured) must never be overwritten.
        if (WindowsUpdateHoldPlanner.HasUnownedTargetReleaseValue(snapshot))
        {
            return WindowsUpdateOperationResult.Failed;
        }

        var desired = WindowsUpdateHoldPlanner.DesiredValues(detection.Info);

        // Idempotency: already exactly the desired state → nothing to do.
        if (WindowsUpdateHoldPlanner.MatchesDesired(snapshot, detection.Info))
        {
            return WindowsUpdateOperationResult.Success;
        }

        var merged = MergeRecords(snapshot);

        // Snapshot the previous state of every value about to be written,
        // BEFORE the first write, so an interrupted run can always roll back.
        foreach (var value in desired)
        {
            if (!merged.OwnedValues.Any(v => string.Equals(v.Name, value.Name, StringComparison.OrdinalIgnoreCase)))
            {
                var current = value.Name switch
                {
                    var n when n == WindowsUpdateHoldPlanner.ProductVersionName => snapshot.ProductVersionValue,
                    var n when n == WindowsUpdateHoldPlanner.TargetReleaseVersionName => snapshot.TargetReleaseVersionValue,
                    var n when n == WindowsUpdateHoldPlanner.TargetReleaseVersionInfoName => snapshot.TargetReleaseVersionInfoValue,
                    _ => null
                };

                merged.OwnedValues.Add(new WindowsUpdateOwnedValue
                {
                    Name = value.Name,
                    PreviousKind = current?.Kind ?? WindowsUpdatePreviousValueKind.Absent,
                    PreviousDword = current?.Dword,
                    PreviousString = current?.Text
                });
            }
        }

        merged.SchemaVersion = WindowsUpdateChangeRecord.CurrentSchemaVersion;
        merged.PreviousKeyExisted ??= snapshot.KeyExists;
        merged.ChangedAtUtc = DateTime.UtcNow;

        if (!TrySaveRecord(merged))
        {
            return WindowsUpdateOperationResult.Failed;
        }

        var commands = WindowsUpdateHoldCommandBuilder.BuildApply(desired);
        if (commands.Count == 0)
        {
            return WindowsUpdateOperationResult.Failed;
        }

        foreach (var command in commands)
        {
            DiagnosticLog($"apply-begin file={command.FileName} args={command.Arguments}");
            var run = _runElevated(command);
            if (run is null)
            {
                // User canceled the elevation prompt — no partial writes from us.
                DiagnosticLog("apply-aborted uac-canceled");
                return WindowsUpdateOperationResult.Canceled;
            }

            if (run != 0)
            {
                // The elevated operation itself failed. Verify-and-rollback below
                // restores the exact pre-apply state; never report success.
                DiagnosticLog($"apply-nonzero-exit exit={run}");
                break;
            }

            // Per-command read-back: proves whether THIS command wrote its value.
            var name = command.Arguments.Contains("TargetReleaseVersion ")
                ? WindowsUpdateHoldPlanner.TargetReleaseVersionName
                : command.Arguments.Contains("TargetReleaseVersionInfo")
                    ? WindowsUpdateHoldPlanner.TargetReleaseVersionInfoName
                    : WindowsUpdateHoldPlanner.ProductVersionName;
            var readBack = _reader.GetValue(name);
            DiagnosticLog($"apply-readback name={name} present={readBack is not null} kind={readBack?.Kind} value={readBack?.Text ?? readBack?.Dword?.ToString() ?? "null"}");
        }

        // Verify the post-write state; roll back what we can if incomplete.
        var after = ReadSnapshot();
        var afterMatches = !after.ReadError && WindowsUpdateHoldPlanner.MatchesDesired(after, detection.Info);
        DiagnosticLog($"apply-verify matches={afterMatches} readError={after.ReadError} product={after.ProductVersionValue is not null} flag={after.TargetReleaseVersionValue is not null} info={after.TargetReleaseVersionInfoValue is not null} record={(after.OwnedRecord is not null ? "present" : "absent")}");
        if (afterMatches)
        {
            return WindowsUpdateOperationResult.Success;
        }

        // Partial failure: attempt best-effort rollback of exactly what we own.
        DiagnosticLog("apply-rollback-begin");
        RollbackOwnedValues(merged);
        DiagnosticLog("apply-rollback-done");

        // If the rollback provably restored the pre-apply state, drop the
        // ownership record so the machine is not left in a half-owned state.
        var afterRollback = ReadSnapshot();
        if (!afterRollback.ReadError
            && !WindowsUpdateHoldPlanner.OwnedValueNames.Any(name => afterRollback.HasValue(name)))
        {
            _recordStore.Delete();
        }

        return WindowsUpdateOperationResult.Failed;
    }

    // --------------------------------------------------------------- rollback

    private WindowsUpdateOperationResult RollbackCore()
    {
        var snapshot = ReadSnapshot();
        if (snapshot.ReadError)
        {
            return WindowsUpdateOperationResult.Failed;
        }

        var record = snapshot.OwnedRecord;
        if (record is null || !WindowsUpdateHoldPlanner.OwnedRecordCoversAll(snapshot))
        {
            // Nothing owned to remove — removing nothing is the idempotent no-op.
            return WindowsUpdateOperationResult.Success;
        }

        // An unowned target-release policy alongside ours must not be deleted;
        // removing only our owned values is still exact.
        RollbackOwnedValues(record);

        var command = WindowsUpdateHoldCommandBuilder.BuildCleanup(record);
        if (command is not null && _runElevated(command) != 0)
        {
            // Cleanup is cosmetic (removing an empty TrafficLens-created key);
            // a failure here must not mask the rollback result below.
        }

        var after = ReadSnapshot();
        if (after.ReadError)
        {
            return WindowsUpdateOperationResult.Failed;
        }

        var remainingOwned = WindowsUpdateHoldPlanner.OwnedValueNames
            .Any(name => _reader.GetValue(name) is not null && OwnsName(record, name));

        if (!remainingOwned)
        {
            _recordStore.Delete();
            return WindowsUpdateOperationResult.Success;
        }

        return WindowsUpdateOperationResult.Failed;
    }

    /// <summary>
    /// Restores exactly the pre-change state of every value the record owns.
    /// Restores are executed through the one-shot elevated command path (the
    /// policy key usually needs elevation), with the in-process applier as a
    /// best-effort fallback when no command can be built.
    /// </summary>
    private void RollbackOwnedValues(WindowsUpdateChangeRecord record)
    {
        foreach (var owned in record.OwnedValues)
        {
            WindowsUpdateRegistryCommand command;
            if (owned.PreviousKind == WindowsUpdatePreviousValueKind.Absent)
            {
                // The value did not exist before TrafficLens — it must be
                // DELETED. A silent in-process delete cannot write HKLM from
                // the non-elevated app (WUI-014 live release failure): every
                // rollback operation goes through the elevated runner.
                command = WindowsUpdateHoldCommandBuilder.BuildDelete(owned.Name);
            }
            else
            {
                command = WindowsUpdateHoldCommandBuilder.BuildRestore(owned)!.Value.command;
            }

            DiagnosticLog($"rollback-begin file={command.FileName} args={command.Arguments}");
            var run = _runElevated(command);
            if (run is null)
            {
                DiagnosticLog($"rollback-uac-canceled name={owned.Name}");
                continue;
            }

            var stillThere = _reader.GetValue(owned.Name);
            DiagnosticLog($"rollback-readback name={owned.Name} exit={run} present={stillThere is not null}");
        }
    }

    // --------------------------------------------------------------- migration

    /// <summary>
    /// Migration from the legacy NoAutoUpdate behavior: if a legacy record
    /// proves TrafficLens itself changed NoAutoUpdate, its previous state is
    /// carried into the new ownership record and restored exactly on the next
    /// rollback. Ownership that cannot be proven (no record, or a record that
    /// does not cover NoAutoUpdate) is never destructively altered — the
    /// resolver surfaces it as an external policy instead. Schema-1 records
    /// only ever covered NoAutoUpdate, so any schema-1 record is ownership
    /// proof — including the "value was absent before TrafficLens" case.
    /// </summary>
    internal static bool LegacyRecordCoversNoAutoUpdate(WindowsUpdateChangeRecord? legacy)
    {
        if (legacy is null)
        {
            return false;
        }

        if (legacy.OwnedValues.Any(v => string.Equals(v.Name, NoAutoUpdateName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Schema-1 records stored NoAutoUpdate's previous state in the
        // record-level fields (PreviousKind/PreviousDword/PreviousString).
        return legacy.IsLegacy;
    }

    private WindowsUpdateChangeRecord MergeRecords(WindowsUpdateHoldSnapshot snapshot)
    {
        var legacy = snapshot.LegacyRecord;
        var record = new WindowsUpdateChangeRecord();

        // Carry legacy NoAutoUpdate ownership into the new record so the
        // rollback below restores it exactly, then add the target-release
        // values. The desired state never includes NoAutoUpdate itself; the
        // restore writes back what existed BEFORE TrafficLens touched it.
        if (legacy is not null)
        {
            foreach (var owned in legacy.OwnedValues)
            {
                record.OwnedValues.Add(new WindowsUpdateOwnedValue
                {
                    Name = owned.Name,
                    PreviousKind = owned.PreviousKind,
                    PreviousDword = owned.PreviousDword,
                    PreviousString = owned.PreviousString
                });
            }

            if (legacy.IsLegacy
                && !record.OwnedValues.Any(v => v.Name == NoAutoUpdateName)
                && LegacyRecordCoversNoAutoUpdate(legacy))
            {
                record.OwnedValues.Add(new WindowsUpdateOwnedValue
                {
                    Name = NoAutoUpdateName,
                    PreviousKind = legacy.PreviousKind,
                    PreviousDword = legacy.PreviousDword,
                    PreviousString = legacy.PreviousString
                });
            }

            record.PreviousKeyExisted = legacy.PreviousKeyExisted;
        }

        return record;
    }

    // ----------------------------------------------------------------- snapshot

    private WindowsUpdateHoldSnapshot ReadSnapshot()
    {
        try
        {
            var product = _reader.GetValue(WindowsUpdateHoldPlanner.ProductVersionName);
            var flag = _reader.GetValue(WindowsUpdateHoldPlanner.TargetReleaseVersionName);
            var info = _reader.GetValue(WindowsUpdateHoldPlanner.TargetReleaseVersionInfoName);
            var noAuto = _reader.GetValue(NoAutoUpdateName);
            var record = _recordStore.Load();

            var ownedRecord = record is { IsLegacy: false } ? record : null;
            var legacyRecord = record is { IsLegacy: true } ? record : null;

            var ownedNames = ownedRecord is not null
                ? ownedRecord.OwnedValues.Select(v => v.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var others = _reader.ValueNames
                .Where(n => !ownedNames.Contains(n))
                .Where(n => !string.Equals(n, WindowsUpdateHoldPlanner.ProductVersionName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(n, WindowsUpdateHoldPlanner.TargetReleaseVersionName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(n, WindowsUpdateHoldPlanner.TargetReleaseVersionInfoName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(n, NoAutoUpdateName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return new WindowsUpdateHoldSnapshot
            {
                ProductVersionValue = product,
                TargetReleaseVersionValue = flag,
                TargetReleaseVersionInfoValue = info,
                OtherValueNames = others,
                KeyExists = _reader.KeyExists,
                NoAutoUpdateValue = noAuto,
                LegacyRecord = legacyRecord,
                OwnedRecord = ownedRecord,
                VersionDetection = _versionDetector.Detect()
            };
        }
        catch (Exception ex)
        {
            return new WindowsUpdateHoldSnapshot
            {
                ReadError = true,
                ErrorMessage = ex.Message
            };
        }
    }

    private static bool OwnsName(WindowsUpdateChangeRecord record, string name) =>
        record.OwnedValues.Any(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

    private bool TrySaveRecord(WindowsUpdateChangeRecord record) => _recordStore.Save(record);

    // ------------------------------------------------------------------ elevation

    private static RegistryKey? DefaultOpenAuKey() =>
        Registry.LocalMachine.OpenSubKey(AuKeyPath, false);

    /// <summary>
    /// Runs the one-shot elevated command. Returns the exit code, or null when
    /// the user canceled the elevation prompt.
    /// </summary>
    private static int? RunElevatedProcess(WindowsUpdateRegistryCommand? command)
    {
        if (command is null)
        {
            return 0;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command.FileName,
                Arguments = command.Arguments,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var startedAt = DateTime.UtcNow;
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                DiagnosticLog($"start-returned-null file={command.FileName}");
                return -1;
            }

            process.WaitForExit();
            var exit = process.ExitCode;
            DiagnosticLog($"exit={exit} pid={process.Id} file={command.FileName} args={command.Arguments} waitedMs={(int)(DateTime.UtcNow - startedAt).TotalMilliseconds}");
            return exit;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            DiagnosticLog($"uac-canceled file={command.FileName} args={command.Arguments}");
            return null;
        }
        catch (Exception ex)
        {
            DiagnosticLog($"exception type={ex.GetType().Name} msg={ex.Message} file={command.FileName} args={command.Arguments}");
            return -1;
        }
    }

    /// <summary>
    /// WUI-014 live-failure diagnostics: one line per elevated operation,
    /// appended to a dedicated file next to the app logs. Records the exact
    /// executable, argument shape, child PID and exit code so the real
    /// execution can be reconstructed. Contains no secrets.
    /// </summary>
    internal static void DiagnosticLog(string message)
    {
        try
        {
            var line = $"{DateTime.UtcNow:O} {message}";
            File.AppendAllText(
                Path.Combine(AppPaths.RootDirectory, "windowsupdate-diag.log"),
                line + Environment.NewLine);
        }
        catch
        {
            // Diagnostics must never break the operation.
        }
    }
}
