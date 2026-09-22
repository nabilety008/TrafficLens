using System.Globalization;
using System.Resources;
using TrafficLens.Core.Localization;

namespace TrafficLens.WinUI;

public sealed class LocalizationService : ILocalizationService
{
    private static readonly ResourceManager ResourceManager =
        new("TrafficLens.WinUI.Resources.Strings", typeof(LocalizationService).Assembly);

    private static readonly HashSet<string> RightToLeftCultures = new(StringComparer.OrdinalIgnoreCase)
    {
        "fa", "fa-IR", "ar", "ar-SA", "he", "he-IL", "ur", "ur-PK"
    };

    private CultureInfo _currentCulture = CultureInfo.GetCultureInfo("en-US");

    public CultureInfo CurrentCulture => _currentCulture;

    public event EventHandler? CultureChanged;

    public bool IsRightToLeft { get; private set; }

    public string this[string key] => GetString(key);

    public string GetString(string key, string? cultureName = null)
    {
        var culture = cultureName is null
            ? _currentCulture
            : CultureInfo.GetCultureInfo(cultureName);

        return ResourceManager.GetString(key, culture) ?? $"[{key}]";
    }

    public void SetCulture(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        _currentCulture = culture;
        IsRightToLeft = RightToLeftCultures.Contains(culture.Name);
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }
}
