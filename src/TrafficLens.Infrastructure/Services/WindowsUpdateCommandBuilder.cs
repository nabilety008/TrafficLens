namespace TrafficLens.Infrastructure.Services;

public sealed record WindowsUpdateRegistryCommand(string FileName, string Arguments);

public static class WindowsUpdateCommandBuilder
{
    public const string AutoUpdateKeyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
    public const string AutoUpdateValueName = "NoAutoUpdate";

    private const string RootPrefix = @"HKLM\";

    public static WindowsUpdateRegistryCommand BuildDisable() =>
        new(
            "reg.exe",
            $@"add ""{RootPrefix}{AutoUpdateKeyPath}"" /v {AutoUpdateValueName} /t REG_DWORD /d 1 /f");

    public static WindowsUpdateRegistryCommand BuildEnable(WindowsUpdateChangeRecord record)
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
            _ => new WindowsUpdateRegistryCommand(
                "reg.exe",
                $@"delete ""{key}"" /v {AutoUpdateValueName} /f")
        };
    }
}
