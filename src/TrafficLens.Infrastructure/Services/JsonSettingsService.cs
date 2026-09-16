using System.Text.Json;
using TrafficLens.Core.Abstractions;

namespace TrafficLens.Infrastructure.Services;

public sealed class JsonSettingsService : ISettingsService
{
    public const string SettingsVersion = "1";
    public const string VersionKey = "settings.version";
    public const string LanguageKey = "language";
    public const string StartWithWindowsKey = "StartWithWindows";
    public const string StartMinimizedKey = "StartMinimized";

    private const string DefaultLanguage = "en-US";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly Dictionary<string, string> _values;

    public JsonSettingsService(string filePath)
    {
        _filePath = filePath;
        _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Load();
        if (string.IsNullOrEmpty(Get(VersionKey, string.Empty)))
        {
            Set(VersionKey, SettingsVersion);
        }
    }

    public string Language
    {
        get => Get(LanguageKey, DefaultLanguage);
        set => Set(LanguageKey, value);
    }

    public string Get(string key, string defaultValue)
    {
        return _values.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public void Set(string key, string value)
    {
        _values[key] = value;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        WriteAtomic(_filePath, JsonSerializer.Serialize(_values, SerializerOptions));
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(_filePath),
                SerializerOptions);

            if (loaded is not null)
            {
                foreach (var (key, value) in loaded)
                {
                    _values[key] = value;
                }
            }
        }
        catch
        {
        }
    }

    private static void WriteAtomic(string filePath, string content)
    {
        var temporaryPath = filePath + ".tmp";

        try
        {
            using (var stream = File.CreateText(temporaryPath))
            {
                stream.Write(content);
                stream.Flush();
                stream.Close();
            }

            File.Move(temporaryPath, filePath, true);
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
            }

            throw;
        }
    }
}