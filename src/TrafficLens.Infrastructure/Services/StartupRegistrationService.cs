using System.Runtime.Versioning;
using Microsoft.Win32;
using TrafficLens.Core.Abstractions;

namespace TrafficLens.Infrastructure.Services;

[SupportedOSPlatform("windows")]
public sealed class StartupRegistrationService : IStartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TrafficLens";

    private readonly string _executablePath;
    private readonly string? _error;

    public StartupRegistrationService()
    {
        _executablePath = Environment.ProcessPath ?? string.Empty;
        _error = string.IsNullOrEmpty(_executablePath) ? "Cannot determine executable path" : null;
    }

    public string? LastError => _error;

    public bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrEmpty(value) && value.Contains("TrafficLens", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void Enable(bool startMinimized)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key is null) return;

            var exePath = _executablePath;
            if (exePath.Contains(' '))
            {
                exePath = $"\"{exePath}\"";
            }

            var command = startMinimized
                ? $"{exePath} --minimized"
                : exePath;

            key.SetValue(ValueName, command, RegistryValueKind.String);
        }
        catch
        {
        }
    }

    public void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key is null) return;

            var value = key.GetValue(ValueName) as string;
            if (value is not null && value.Contains("TrafficLens", StringComparison.OrdinalIgnoreCase))
            {
                key.DeleteValue(ValueName, false);
            }
        }
        catch
        {
        }
    }
}
