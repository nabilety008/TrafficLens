using TrafficLens.Core.Abstractions;

namespace TrafficLens.WinUI.Tests;

internal sealed class RecordingSettingsService : ISettingsService
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public string Language { get; set; } = "en-US";

    public bool SettingsFileExisted { get; set; } = true;

    public int SaveCalls { get; private set; }

    public int SetCalls { get; private set; }

    public List<string> Writes { get; } = new();

    public string Get(string key, string defaultValue) =>
        _values.TryGetValue(key, out var value) ? value : defaultValue;

    public void Set(string key, string value)
    {
        _values[key] = value;
        SetCalls++;
        Writes.Add($"{key}={value}");
    }

    public void Save() => SaveCalls++;
}
