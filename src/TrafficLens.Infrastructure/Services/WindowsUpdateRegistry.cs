using System.Runtime.Versioning;
using Microsoft.Win32;

namespace TrafficLens.Infrastructure.Services;

/// <summary>A read value of the WindowsUpdate policy key.</summary>
public sealed class WindowsUpdateRegistryValue
{
    public WindowsUpdateRegistryValue(WindowsUpdatePreviousValueKind kind, int? dword, string? text)
    {
        Kind = kind;
        Dword = dword;
        Text = text;
    }

    public WindowsUpdatePreviousValueKind Kind { get; }

    public int? Dword { get; }

    public string? Text { get; }
}

/// <summary>
/// Read/observe side of the WindowsUpdate policy key. Pure observation — never
/// writes. Implementations must tolerate a missing key.
/// </summary>
public interface IWindowsUpdatePolicyReader
{
    /// <summary>True when the WindowsUpdate policy key exists.</summary>
    bool KeyExists { get; }

    /// <summary>Names of all values currently in the WindowsUpdate policy key.</summary>
    IReadOnlyList<string> ValueNames { get; }

    /// <summary>Reads one value; null when absent or an unsupported type.</summary>
    WindowsUpdateRegistryValue? GetValue(string name);
}

/// <summary>
/// Mutation side of the WindowsUpdate policy key. Every write is one-shot and
/// user-triggered; TrafficLens never rewrites values on a schedule.
/// </summary>
public interface IWindowsUpdatePolicyApplier
{
    /// <summary>Writes one DWORD value under the WindowsUpdate policy key (creates the key if needed).</summary>
    bool WriteDword(string name, int value);

    /// <summary>Writes one string value under the WindowsUpdate policy key (creates the key if needed).</summary>
    bool WriteString(string name, string value);

    /// <summary>Deletes one value; returns true when the value is gone afterwards.</summary>
    bool DeleteValue(string name);
}

[SupportedOSPlatform("windows")]
public sealed class WindowsUpdatePolicyRegistry : IWindowsUpdatePolicyReader, IWindowsUpdatePolicyApplier
{
    public const string PolicyKeyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";

    private readonly Func<RegistryKey?> _openPolicyKeyWritable;
    private readonly Func<RegistryKey?> _openPolicyKeyReadOnly;

    public WindowsUpdatePolicyRegistry()
        : this(
            () => Registry.LocalMachine.OpenSubKey(PolicyKeyPath, writable: true),
            () => Registry.LocalMachine.OpenSubKey(PolicyKeyPath, writable: false))
    {
    }

    internal WindowsUpdatePolicyRegistry(
        Func<RegistryKey?> openPolicyKeyWritable,
        Func<RegistryKey?>? openPolicyKeyReadOnly = null)
    {
        _openPolicyKeyWritable = openPolicyKeyWritable;
        _openPolicyKeyReadOnly = openPolicyKeyReadOnly ?? openPolicyKeyWritable;
    }

    public bool KeyExists
    {
        get
        {
            using var key = _openPolicyKeyReadOnly();
            return key is not null;
        }
    }

    public IReadOnlyList<string> ValueNames
    {
        get
        {
            using var key = _openPolicyKeyReadOnly();
            if (key is null)
            {
                return Array.Empty<string>();
            }

            return key.GetValueNames().Where(n => n.Length > 0).ToArray();
        }
    }

    public WindowsUpdateRegistryValue? GetValue(string name)
    {
        using var key = _openPolicyKeyReadOnly();
        if (key is null)
        {
            return null;
        }

        var raw = key.GetValue(name);
        return raw switch
        {
            int dword => new WindowsUpdateRegistryValue(WindowsUpdatePreviousValueKind.Dword, dword, null),
            string text => new WindowsUpdateRegistryValue(WindowsUpdatePreviousValueKind.String, null, text),
            _ => null
        };
    }

    public bool WriteDword(string name, int value)
    {
        try
        {
            using var key = _openPolicyKeyWritable() ?? CreatePolicyKey();
            if (key is null)
            {
                return false;
            }

            key.SetValue(name, value, RegistryValueKind.DWord);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool WriteString(string name, string value)
    {
        try
        {
            using var key = _openPolicyKeyWritable() ?? CreatePolicyKey();
            if (key is null)
            {
                return false;
            }

            key.SetValue(name, value, RegistryValueKind.String);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool DeleteValue(string name)
    {
        try
        {
            using var key = _openPolicyKeyWritable();
            if (key is null)
            {
                return true;
            }

            if (key.GetValue(name) is null)
            {
                return true;
            }

            key.DeleteValue(name, throwOnMissingValue: false);
            return key.GetValue(name) is null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static RegistryKey? CreatePolicyKey()
    {
        try
        {
            return Registry.LocalMachine.CreateSubKey(PolicyKeyPath, true);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
