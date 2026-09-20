using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;
using TrafficLens.Infrastructure.Services;

namespace TrafficLens.App.Tests;

public sealed class OnboardingViewModelTests : IDisposable
{
    private readonly FakeSettingsService _settings = new();
    private readonly LocalizationService _localization = new();
    private readonly OnboardingViewModel _vm;

    public OnboardingViewModelTests()
    {
        _localization.SetCulture("en-US");
        _vm = new OnboardingViewModel(_localization, _settings);
    }

    public void Dispose() => _vm.Dispose();

    [Fact]
    public void FreshProfile_AutoShowsOnStartup()
    {
        _settings.SettingsFileExisted = false;

        _vm.AutoShowIfRequired();

        Assert.True(_vm.IsVisible);
    }

    [Fact]
    public void Dismiss_PersistsCompletionAndHides()
    {
        _settings.SettingsFileExisted = false;
        _vm.AutoShowIfRequired();
        Assert.True(_vm.IsVisible);

        _vm.DismissCommand.Execute(null);

        Assert.False(_vm.IsVisible);
        Assert.Equal("True", _settings.Get(OnboardingSettings.CompletedKey, string.Empty));
        Assert.True(_settings.SaveCalls >= 1);
    }

    [Fact]
    public void CompletedOnboarding_NoAutoShowOnRestart()
    {
        _settings.SettingsFileExisted = false;
        _vm.AutoShowIfRequired();
        _vm.DismissCommand.Execute(null);

        using var restart = new OnboardingViewModel(_localization, _settings);
        restart.AutoShowIfRequired();

        Assert.False(restart.IsVisible);
    }

    [Fact]
    public void ExistingProfile_WithoutFlag_NoAutoShow()
    {
        _settings.SettingsFileExisted = true;
        _settings.Set(JsonSettingsService.LanguageKey, "fa-IR");

        _vm.AutoShowIfRequired();

        Assert.False(_vm.IsVisible);
        Assert.Equal(string.Empty, _settings.Get(OnboardingSettings.CompletedKey, string.Empty));
    }

    [Fact]
    public void ManualReopen_ShowsAfterCompletion_WithoutResettingState()
    {
        _settings.SettingsFileExisted = false;
        _vm.AutoShowIfRequired();
        _vm.DismissCommand.Execute(null);
        Assert.False(_vm.IsVisible);

        var saveCallsBefore = _settings.SaveCalls;
        _vm.Show();

        Assert.True(_vm.IsVisible);
        Assert.Equal("True", _settings.Get(OnboardingSettings.CompletedKey, string.Empty));
        Assert.Equal(saveCallsBefore, _settings.SaveCalls);
    }

    [Fact]
    public void ManualReopen_DoesNotMarkCompletedTwice()
    {
        _vm.Show();
        var saveCallsBefore = _settings.SaveCalls;

        _vm.DismissCommand.Execute(null);

        Assert.Equal(saveCallsBefore + 1, _settings.SaveCalls);
    }

    [Fact]
    public void DismissWhileHidden_DoesNothing()
    {
        var saveCallsBefore = _settings.SaveCalls;

        _vm.DismissCommand.Execute(null);

        Assert.False(_vm.IsVisible);
        Assert.Equal(string.Empty, _settings.Get(OnboardingSettings.CompletedKey, string.Empty));
        Assert.Equal(saveCallsBefore, _settings.SaveCalls);
    }

    [Fact]
    public void EnglishCulture_PopulatesLocalizedStrings()
    {
        _vm.Show();

        Assert.Equal("Get started with TrafficLens", _vm.GetStartedTitleLabel);
        Assert.Equal("Applications", _vm.ApplicationsTopicLabel);
        Assert.Equal("VPNs and tunnels", _vm.TunnelsTopicLabel);
        Assert.Equal("System tray", _vm.TrayTopicLabel);
        Assert.Equal("Floating widget", _vm.WidgetTopicLabel);
        Assert.Equal("Alerts", _vm.AlertsTopicLabel);
        Assert.Equal("Language", _vm.LanguageTopicLabel);
        Assert.Equal("Got it", _vm.DismissLabel);
    }

    [Fact]
    public void PersianCulture_PopulatesPersianStrings()
    {
        _localization.SetCulture("fa-IR");
        _vm.Show();

        Assert.Equal("شروع کار با ترافیک‌لنز", _vm.GetStartedTitleLabel);
        Assert.Equal("برنامه‌ها", _vm.ApplicationsTopicLabel);
        Assert.Contains("تونل", _vm.TunnelsTopicText);
        Assert.Equal("متوجه شدم", _vm.DismissLabel);
    }

    [Fact]
    public void CultureSwitch_RelocalizesOpenPanel()
    {
        _vm.Show();

        Assert.Equal("Got it", _vm.DismissLabel);

        _localization.SetCulture("fa-IR");

        Assert.Equal("متوجه شدم", _vm.DismissLabel);
        Assert.Equal("شروع کار با ترافیک‌لنز", _vm.GetStartedTitleLabel);
        Assert.True(_vm.IsVisible);
    }
}