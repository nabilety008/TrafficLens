namespace TrafficLens.Infrastructure.Services;

public sealed record WindowsUpdateRegistryCommand(string FileName, string Arguments);

/// <summary>
/// Builds the one-shot elevated registry commands for the target-feature-update
/// hold. Only the three supported target-release values are ever written;
/// NoAutoUpdate is never written as a desired state.
/// </summary>
public static class WindowsUpdateHoldCommandBuilder
{
    private const string RootPrefix = @"HKLM\";
    public const string PolicyKeyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";

    /// <summary>One reg.exe command applying every desired value atomically.</summary>
    public static WindowsUpdateRegistryCommand? BuildApply(
        IReadOnlyList<WindowsUpdateHoldPlanner.DesiredValue> desired)
    {
        if (desired.Count == 0)
        {
            return null;
        }

        var parts = desired.Select(value =>
        {
            var fullKey = $@"""{RootPrefix}{PolicyKeyPath}""";
            return value.DesiredKind == WindowsUpdatePreviousValueKind.Dword
                ? $@"add {fullKey} /v {value.Name} /t REG_DWORD /d {value.DesiredDword} /f"
                : $@"add {fullKey} /v {value.Name} /t REG_SZ /d ""{value.DesiredString}"" /f";
        });

        return new WindowsUpdateRegistryCommand("reg.exe", string.Join(" ; ", parts));
    }

    /// <summary>
    /// Command restoring one previously-existing value. Returns null when the
    /// value did not exist before TrafficLens (it must be deleted, not written).
    /// </summary>
    public static (WindowsUpdatePreviousValueKind kind, WindowsUpdateRegistryCommand command)? BuildRestore(
        WindowsUpdateOwnedValue owned)
    {
        if (owned.PreviousKind == WindowsUpdatePreviousValueKind.Absent)
        {
            return null;
        }

        var fullKey = $@"""{RootPrefix}{PolicyKeyPath}""";
        var command = owned.PreviousKind == WindowsUpdatePreviousValueKind.Dword
            ? new WindowsUpdateRegistryCommand(
                "reg.exe",
                $@"add {fullKey} /v {owned.Name} /t REG_DWORD /d {owned.PreviousDword ?? 0} /f")
            : new WindowsUpdateRegistryCommand(
                "reg.exe",
                $@"add {fullKey} /v {owned.Name} /t REG_SZ /d ""{owned.PreviousString ?? string.Empty}"" /f");

        return (owned.PreviousKind, command);
    }

    /// <summary>
    /// Optional post-rollback cleanup: removes the WindowsUpdate policy key only
    /// when TrafficLens created it AND it is completely empty afterwards. An
    /// empty key created by TrafficLens is harmless, but removing it restores
    /// the exact pre-TrafficLens state.
    /// </summary>
    public static WindowsUpdateRegistryCommand? BuildCleanup(WindowsUpdateChangeRecord record)
    {
        if (record.PreviousKeyExisted != false)
        {
            return null;
        }

        var psKeyPath = @"HKLM:\" + PolicyKeyPath;
        var script =
            "$k='" + psKeyPath + "'; " +
            "if (Test-Path -LiteralPath $k) { $i=Get-Item -LiteralPath $k; if ($i.ValueCount -eq 0 -and $i.SubKeyCount -eq 0) { Remove-Item -LiteralPath $k -Force -ErrorAction SilentlyContinue } }";

        return new WindowsUpdateRegistryCommand("powershell.exe", "-NoProfile -NonInteractive -Command \"" + script + "\"");
    }
}
