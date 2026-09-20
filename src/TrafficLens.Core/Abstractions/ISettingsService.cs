namespace TrafficLens.Core.Abstractions;

public interface ISettingsService
{
    string Language { get; set; }

    /// <summary>
    /// True when a settings file already existed when this service was created.
    /// Lets callers distinguish a brand-new profile from an upgrading one.
    /// </summary>
    bool SettingsFileExisted { get; }

    string Get(string key, string defaultValue);

    void Set(string key, string value);

    void Save();
}