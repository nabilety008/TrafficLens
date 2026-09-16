using TrafficLens.Infrastructure.Services;

namespace TrafficLens.Infrastructure.Tests;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _directory;
    private readonly string _filePath;

    public JsonSettingsServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "trafficlens-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _filePath = Path.Combine(_directory, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public void MissingFile_ReturnsDefaults()
    {
        var service = new JsonSettingsService(_filePath);

        Assert.Equal("en-US", service.Language);
        Assert.Equal(string.Empty, service.Get("does.not.exist", string.Empty));
        Assert.False(GetBool(service, JsonSettingsService.StartWithWindowsKey, false));
    }

    [Fact]
    public void MissingFile_LoadsVersionConstant()
    {
        var service = new JsonSettingsService(_filePath);

        Assert.Equal(JsonSettingsService.SettingsVersion, service.Get(JsonSettingsService.VersionKey, string.Empty));
    }

    [Fact]
    public void SaveAndReload_RoundTrips()
    {
        var service = new JsonSettingsService(_filePath);
        service.Set(JsonSettingsService.LanguageKey, "fa-IR");
        service.Set(JsonSettingsService.StartMinimizedKey, "True");
        service.Save();

        var reloaded = new JsonSettingsService(_filePath);
        Assert.Equal("fa-IR", reloaded.Get(JsonSettingsService.LanguageKey, string.Empty));
        Assert.True(GetBool(reloaded, JsonSettingsService.StartMinimizedKey, false));
    }

    [Fact]
    public void Save_PreservesUnknownKeys()
    {
        var service = new JsonSettingsService(_filePath);
        service.Set("someVendorOption", "keep-me");
        service.Set(JsonSettingsService.StartWithWindowsKey, "True");
        service.Save();

        var reloaded = new JsonSettingsService(_filePath);
        Assert.Equal("keep-me", reloaded.Get("someVendorOption", string.Empty));
        Assert.True(GetBool(reloaded, JsonSettingsService.StartWithWindowsKey, false));

        reloaded.Set(JsonSettingsService.StartMinimizedKey, "True");
        reloaded.Save();

        var reread = new JsonSettingsService(_filePath);
        Assert.Equal("keep-me", reread.Get("someVendorOption", string.Empty));
        Assert.True(GetBool(reread, JsonSettingsService.StartWithWindowsKey, false));
        Assert.True(GetBool(reread, JsonSettingsService.StartMinimizedKey, false));
    }

    [Fact]
    public void ChangingOneKey_DoesNotDropOthers()
    {
        var service = new JsonSettingsService(_filePath);
        service.Set(JsonSettingsService.LanguageKey, "fa-IR");
        service.Set(JsonSettingsService.StartWithWindowsKey, "True");
        service.Save();

        var update = new JsonSettingsService(_filePath);
        update.Set("MinimizeToTray", "False");
        update.Save();

        var final = new JsonSettingsService(_filePath);
        Assert.Equal("fa-IR", final.Get(JsonSettingsService.LanguageKey, string.Empty));
        Assert.True(GetBool(final, JsonSettingsService.StartWithWindowsKey, false));
        Assert.False(GetBool(final, "MinimizeToTray", true));
    }

    [Fact]
    public void MalformedJson_FallsBackToDefaultsWithoutCrashing()
    {
        File.WriteAllText(_filePath, "{ not valid json !!!");

        var service = new JsonSettingsService(_filePath);

        Assert.Equal("en-US", service.Get(JsonSettingsService.LanguageKey, "en-US"));
        Assert.False(GetBool(service, JsonSettingsService.StartMinimizedKey, false));
    }

    [Fact]
    public void EmptyFile_FallsBackToDefaults()
    {
        File.WriteAllText(_filePath, string.Empty);

        var service = new JsonSettingsService(_filePath);

        Assert.Equal("en-US", service.Get(JsonSettingsService.LanguageKey, "en-US"));
    }

    [Fact]
    public void EarlierMinimalFile_WithOnlyTrayKeys_LoadsDefaultsForMissingKeys()
    {
        File.WriteAllText(_filePath, """{ "TrayCloseNoticeShown": "True", "CloseToTray": "True" }""");

        var service = new JsonSettingsService(_filePath);

        Assert.Equal("True", service.Get("TrayCloseNoticeShown", string.Empty));
        Assert.Equal("True", service.Get("CloseToTray", string.Empty));
        Assert.Equal("en-US", service.Get(JsonSettingsService.LanguageKey, "en-US"));
        Assert.False(GetBool(service, JsonSettingsService.StartWithWindowsKey, false));
    }

    [Fact]
    public void Save_IsAtomic_NoTempFileLeftBehind()
    {
        var service = new JsonSettingsService(_filePath);
        service.Set(JsonSettingsService.StartWithWindowsKey, "True");
        service.Save();
        var second = new JsonSettingsService(_filePath);
        second.Set(JsonSettingsService.StartMinimizedKey, "True");
        second.Save();

        Assert.Single(Directory.GetFiles(_directory, "*.json"));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void Keys_AreCaseInsensitive()
    {
        var service = new JsonSettingsService(_filePath);
        service.Set("startwithwindows", "True");

        Assert.True(GetBool(service, JsonSettingsService.StartWithWindowsKey, false));
    }

    [Fact]
    public void LanguageProperty_ReadsAndWrites()
    {
        var service = new JsonSettingsService(_filePath);
        service.Language = "fa-IR";

        Assert.Equal("fa-IR", service.Get(JsonSettingsService.LanguageKey, string.Empty));
        Assert.Equal("fa-IR", service.Language);
    }

    private static bool GetBool(TrafficLens.Core.Abstractions.ISettingsService settings, string key, bool defaultValue)
    {
        var text = settings.Get(key, string.Empty);
        return string.IsNullOrEmpty(text) ? defaultValue : bool.TryParse(text, out var value) && value;
    }
}