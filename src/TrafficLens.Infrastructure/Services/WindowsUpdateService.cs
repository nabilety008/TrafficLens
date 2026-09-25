using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Win32;
using TrafficLens.Core.Abstractions;

namespace TrafficLens.Infrastructure.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsUpdateService : IWindowsUpdateService
{
    private const string WindowsUpdatePolicyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
    private const string ServiceKeyPath = @"SYSTEM\CurrentControlSet\Services\wuauserv";
    private const int ServiceDisabledValue = 4;

    private static readonly string RecordFile = Path.Combine(AppPaths.RootDirectory, "windowsupdate-state.json");

    public WindowsUpdateState GetState() => WindowsUpdateStateResolver.Resolve(ReadSnapshot());

    public Task<WindowsUpdateOperationResult> DisableAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => DisableCore(), cancellationToken);

    public Task<WindowsUpdateOperationResult> EnableAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => EnableCore(), cancellationToken);

    private WindowsUpdateOperationResult DisableCore()
    {
        var snapshot = ReadSnapshot();
        if (snapshot.ReadError)
        {
            return WindowsUpdateOperationResult.Failed;
        }

        if (snapshot.NoAutoUpdatePresent && snapshot.NoAutoUpdateValue == 1)
        {
            return WindowsUpdateOperationResult.Success;
        }

        var record = CreateRecord(snapshot);
        if (!TrySaveRecord(record))
        {
            return WindowsUpdateOperationResult.Failed;
        }

        var run = RunElevated(WindowsUpdateCommandBuilder.BuildDisable());
        if (run.Canceled)
        {
            TryDeleteRecord();
            return WindowsUpdateOperationResult.Canceled;
        }

        var after = ReadSnapshot();
        if (!after.ReadError && after.NoAutoUpdatePresent && after.NoAutoUpdateValue == 1)
        {
            return WindowsUpdateOperationResult.Success;
        }

        if (!after.ReadError)
        {
            TryDeleteRecord();
        }

        return WindowsUpdateOperationResult.Failed;
    }

    private WindowsUpdateOperationResult EnableCore()
    {
        var snapshot = ReadSnapshot();
        if (snapshot.ReadError)
        {
            return WindowsUpdateOperationResult.Failed;
        }

        if (!snapshot.NoAutoUpdatePresent || snapshot.NoAutoUpdateValue != 1)
        {
            if (snapshot.Record is not null)
            {
                TryDeleteRecord();
            }

            return WindowsUpdateOperationResult.Success;
        }

        if (snapshot.Record is null)
        {
            return WindowsUpdateOperationResult.Failed;
        }

        var run = RunElevated(WindowsUpdateCommandBuilder.BuildEnable(snapshot.Record, snapshot));
        if (run.Canceled)
        {
            return WindowsUpdateOperationResult.Canceled;
        }

        var after = ReadSnapshot();
        if (!after.ReadError && (!after.NoAutoUpdatePresent || after.NoAutoUpdateValue != 1))
        {
            TryDeleteRecord();
            return WindowsUpdateOperationResult.Success;
        }

        return WindowsUpdateOperationResult.Failed;
    }

    private static WindowsUpdateChangeRecord CreateRecord(WindowsUpdateSnapshot snapshot)
    {
        var record = new WindowsUpdateChangeRecord
        {
            ChangedAtUtc = DateTime.UtcNow,
            PreviousKeyExisted = snapshot.AuKeyExists
        };

        if (!snapshot.NoAutoUpdatePresent)
        {
            return record;
        }

        if (snapshot.NoAutoUpdateInvalid)
        {
            record.PreviousKind = WindowsUpdatePreviousValueKind.String;
            record.PreviousString = snapshot.NoAutoUpdateRawString;
            return record;
        }

        record.PreviousKind = WindowsUpdatePreviousValueKind.Dword;
        record.PreviousDword = snapshot.NoAutoUpdateValue;
        return record;
    }

    private WindowsUpdateSnapshot ReadSnapshot()
    {
        try
        {
            var noAutoPresent = false;
            var noAutoInvalid = false;
            int? noAutoValue = null;
            string? noAutoRawString = null;
            var otherPolicy = false;
            var auKeyExists = false;
            var auOtherValues = 0;
            var auSubKeys = 0;

            using (var auKey = Registry.LocalMachine.OpenSubKey(WindowsUpdateCommandBuilder.AutoUpdateKeyPath, false))
            {
                if (auKey is not null)
                {
                    auKeyExists = true;
                    auSubKeys = auKey.SubKeyCount;
                    foreach (var name in auKey.GetValueNames())
                    {
                        if (string.Equals(name, WindowsUpdateCommandBuilder.AutoUpdateValueName, StringComparison.OrdinalIgnoreCase))
                        {
                            noAutoPresent = true;
                            var raw = auKey.GetValue(name);
                            if (raw is int dword)
                            {
                                noAutoValue = dword;
                            }
                            else if (raw is string text && int.TryParse(text, out var parsed))
                            {
                                noAutoValue = parsed;
                            }
                            else
                            {
                                noAutoInvalid = true;
                                noAutoRawString = raw as string ?? raw?.ToString();
                            }
                        }
                        else
                        {
                            otherPolicy = true;
                            auOtherValues++;
                        }
                    }
                }
            }

            using (var wuKey = Registry.LocalMachine.OpenSubKey(WindowsUpdatePolicyPath, false))
            {
                if (wuKey is { } key && key.GetValueNames().Length > 0)
                {
                    otherPolicy = true;
                }
            }

            int? serviceStart = null;
            using (var serviceKey = Registry.LocalMachine.OpenSubKey(ServiceKeyPath, false))
            {
                if (serviceKey?.GetValue("Start") is int start)
                {
                    serviceStart = start;
                }
            }

            return new WindowsUpdateSnapshot
            {
                NoAutoUpdatePresent = noAutoPresent,
                NoAutoUpdateValue = noAutoInvalid ? null : noAutoValue,
                NoAutoUpdateInvalid = noAutoInvalid,
                NoAutoUpdateRawString = noAutoRawString,
                OtherPolicyPresent = otherPolicy,
                AuKeyExists = auKeyExists,
                AuOtherValues = auOtherValues,
                AuSubKeys = auSubKeys,
                ServiceStart = serviceStart,
                Record = LoadRecord()
            };
        }
        catch (Exception ex)
        {
            return new WindowsUpdateSnapshot
            {
                ReadError = true,
                ErrorMessage = ex.Message
            };
        }
    }

    private static (bool Canceled, int ExitCode) RunElevated(WindowsUpdateRegistryCommand command)
    {
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

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return (false, -1);
            }

            process.WaitForExit();
            return (false, process.ExitCode);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return (true, -1);
        }
        catch (Exception)
        {
            return (false, -1);
        }
    }

    private static WindowsUpdateChangeRecord? LoadRecord()
    {
        try
        {
            if (!File.Exists(RecordFile))
            {
                return null;
            }

            var json = File.ReadAllText(RecordFile);
            return JsonSerializer.Deserialize<WindowsUpdateChangeRecord>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TrySaveRecord(WindowsUpdateChangeRecord record)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.RootDirectory);
            File.WriteAllText(RecordFile, JsonSerializer.Serialize(record));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void TryDeleteRecord()
    {
        try
        {
            if (File.Exists(RecordFile))
            {
                File.Delete(RecordFile);
            }
        }
        catch (Exception)
        {
        }
    }
}
