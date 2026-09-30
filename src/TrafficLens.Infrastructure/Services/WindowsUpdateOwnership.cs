using System.Runtime.Versioning;
using System.Text.Json;

namespace TrafficLens.Infrastructure.Services;

/// <summary>Kind of a registry value before TrafficLens changed it.</summary>
public enum WindowsUpdatePreviousValueKind
{
    Absent,
    Dword,
    String
}

/// <summary>
/// The exact previous state of one registry value TrafficLens changed.
/// </summary>
public sealed class WindowsUpdateOwnedValue
{
    public string Name { get; set; } = string.Empty;

    public WindowsUpdatePreviousValueKind PreviousKind { get; set; } = WindowsUpdatePreviousValueKind.Absent;

    public int? PreviousDword { get; set; }

    public string? PreviousString { get; set; }
}

/// <summary>
/// Persisted proof of what TrafficLens changed for the feature-update hold:
/// the previous state of every value it wrote, so rollback restores exactly
/// that state and nothing else.
/// </summary>
public sealed class WindowsUpdateChangeRecord
{
    public const int CurrentSchemaVersion = 2;

    /// <summary>
    /// Schema version. Defaults to 1 so records persisted by the legacy
    /// (NoAutoUpdate-era) implementation — which stored no schema field —
    /// deserialize as legacy. New records set this explicitly on save.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    public List<WindowsUpdateOwnedValue> OwnedValues { get; set; } = new();

    /// <summary>True when the policy key TrafficLens wrote did not exist before.</summary>
    public bool? PreviousKeyExisted { get; set; }

    public DateTime ChangedAtUtc { get; set; }

    // ---- legacy schema-1 fields (old NoAutoUpdate behavior); kept so old
    // records can be read and migrated, never to write NoAutoUpdate again.

    public WindowsUpdatePreviousValueKind PreviousKind { get; set; } = WindowsUpdatePreviousValueKind.Absent;

    public int? PreviousDword { get; set; }

    public string? PreviousString { get; set; }

    public bool IsLegacy => SchemaVersion < CurrentSchemaVersion;
}

/// <summary>Load/save helpers for the ownership record. Injectable path for tests.</summary>
public interface IWindowsUpdateRecordStore
{
    WindowsUpdateChangeRecord? Load();

    bool Save(WindowsUpdateChangeRecord record);

    void Delete();
}

[SupportedOSPlatform("windows")]
public sealed class WindowsUpdateRecordStore : IWindowsUpdateRecordStore
{
    private readonly string _file;

    public WindowsUpdateRecordStore(string filePath)
    {
        _file = filePath;
    }

    public WindowsUpdateChangeRecord? Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                return null;
            }

            var json = File.ReadAllText(_file);
            return JsonSerializer.Deserialize<WindowsUpdateChangeRecord>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public bool Save(WindowsUpdateChangeRecord record)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(record));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(_file))
            {
                File.Delete(_file);
            }
        }
        catch (Exception)
        {
        }
    }
}
