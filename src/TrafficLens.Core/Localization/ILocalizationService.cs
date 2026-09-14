using System.Globalization;

namespace TrafficLens.Core.Localization;

public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }

    event EventHandler? CultureChanged;

    string this[string key] { get; }

    bool IsRightToLeft { get; }

    void SetCulture(string cultureName);

    string GetString(string key, string? cultureName = null);
}