namespace TrafficLens.Infrastructure.Services;

public sealed record WindowsUpdateRegistryCommand(string FileName, string Arguments);

public static class WindowsUpdateCommandBuilder
{
    public const string AutoUpdateKeyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
    public const string AutoUpdateValueName = "NoAutoUpdate";

    private const string RootPrefix = @"HKLM\";

    private const string PsKeyPath = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";

    private const string RemoveValueAndEmptyKeyScript =
        "Remove-ItemProperty -LiteralPath '" + PsKeyPath + "' -Name '" + AutoUpdateValueName + "' -Force -ErrorAction SilentlyContinue; " +
        "$k='" + PsKeyPath + "'; " +
        "if (Test-Path -LiteralPath $k) { $i=Get-Item -LiteralPath $k; if ($i.ValueCount -eq 0 -and $i.SubKeyCount -eq 0) { Remove-Item -LiteralPath $k -Force -ErrorAction SilentlyContinue } }";

    public static WindowsUpdateRegistryCommand BuildDisable() =>
        new(
            "reg.exe",
            $@"add ""{RootPrefix}{AutoUpdateKeyPath}"" /v {AutoUpdateValueName} /t REG_DWORD /d 1 /f");

    public static bool ShouldRemoveCreatedEmptyKey(WindowsUpdateChangeRecord record, WindowsUpdateSnapshot snapshot) =>
        record.PreviousKind == WindowsUpdatePreviousValueKind.Absent
        && record.PreviousKeyExisted == false
        && !snapshot.ReadError
        && snapshot.AuKeyExists
        && snapshot.AuOtherValues == 0
        && snapshot.AuSubKeys == 0;

    public static WindowsUpdateRegistryCommand BuildEnable(WindowsUpdateChangeRecord record, WindowsUpdateSnapshot snapshot)
    {
        var key = $@"{RootPrefix}{AutoUpdateKeyPath}";
        return record.PreviousKind switch
        {
            WindowsUpdatePreviousValueKind.Dword => new WindowsUpdateRegistryCommand(
                "reg.exe",
                $@"add ""{key}"" /v {AutoUpdateValueName} /t REG_DWORD /d {record.PreviousDword ?? 0} /f"),
            WindowsUpdatePreviousValueKind.String => new WindowsUpdateRegistryCommand(
                "reg.exe",
                $@"add ""{key}"" /v {AutoUpdateValueName} /t REG_SZ /d ""{record.PreviousString ?? string.Empty}"" /f"),
            _ => ShouldRemoveCreatedEmptyKey(record, snapshot)
                ? new WindowsUpdateRegistryCommand(
                    "powershell.exe",
                    "-NoProfile -NonInteractive -Command \"" + RemoveValueAndEmptyKeyScript + "\"")
                : new WindowsUpdateRegistryCommand(
                    "reg.exe",
                    $@"delete ""{key}"" /v {AutoUpdateValueName} /f")
        };
    }
}
