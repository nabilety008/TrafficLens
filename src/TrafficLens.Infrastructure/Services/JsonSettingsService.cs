using TrafficLens.Core.Abstractions;

namespace TrafficLens.Infrastructure.Services;

public sealed class JsonSettingsService : ISettingsService
{
    private const string LanguageKey = "language";

    private readonly string _filePath;
    private readonly Dictionary<string, string> _values;

    public JsonSettingsService(string filePath)
    {
        _filePath = filePath;
        _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Load();
    }

    public string Language
    {
        get => Get(LanguageKey, "en-US");
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
        File.WriteAllText(_filePath, System.Text.Json.JsonSerializer.Serialize(_values));
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(_filePath),
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (loaded is not null)
            {
                foreach (var (key, value) in loaded)
                {
                    _values[key] = value;
                }
            }
        }
        catch (Exception)
        {
            // Corrupt or unreadable settings are ignored; defaults are used.
        }
    }
}