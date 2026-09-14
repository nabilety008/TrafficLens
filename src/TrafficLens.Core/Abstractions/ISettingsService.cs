namespace TrafficLens.Core.Abstractions;

public interface ISettingsService
{
    string Language { get; set; }

    string Get(string key, string defaultValue);

    void Set(string key, string value);

    void Save();
}