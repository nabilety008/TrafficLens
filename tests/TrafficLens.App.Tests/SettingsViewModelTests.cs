using System.Globalization;
using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;
using TrafficLens.Infrastructure.Services;

namespace TrafficLens.App.Tests;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly FakeSettingsService _settings = new();
    private readonly FakeFloatingWidgetService _widget = new();
    private readonly FakeAlertService _alerts = new();
    private readonly FakeStartupRegistrationService _startup = new();
    private readonly LocalizationService _localization = new();
    private readonly SettingsViewModel _vm;

    public SettingsViewModelTests()
    {
        _localization.SetCulture("en-US");
        _vm = new SettingsViewModel(_localization, _settings, _widget, _alerts, _startup, new OnboardingViewModel(_localization, _settings));
    }

    public void Dispose() => _vm.Dispose();

    private AlertRuleViewModel Rule(string id) => _vm.AlertRules.Single(r => r.Id == id);

    [Fact]
    public void Constructor_WaitsForSave_DoesNotWriteAnything()
    {
        Assert.Equal(0, _settings.SaveCalls);
        Assert.False(_vm.IsDirty);
        Assert.False(_vm.StartWithWindows);
        Assert.Equal("5", _vm.CooldownText);
        Assert.Equal(5, _vm.AlertRules.Count);
    }

    [Fact]
    public void LanguageOptions_FaPersianDisplayName_IsNotMojibake()
    {
        var fa = _vm.LanguageOptions.Single(o => o.Culture == "fa-IR");
        Assert.Equal("فارسی", fa.DisplayName);

        var en = _vm.LanguageOptions.Single(o => o.Culture == "en-US");
        Assert.Equal("English", en.DisplayName);
    }

    [Fact]
    public void Constructor_LoadsPersistedValues()
    {
        _settings.Set(JsonSettingsService.StartWithWindowsKey, "True");
        _settings.Set(JsonSettingsService.StartMinimizedKey, "True");
        _settings.Set(JsonSettingsService.LanguageKey, "fa-IR");
        _settings.Set("MinimizeToTray", "False");

        using var vm = new SettingsViewModel(_localization, _settings, _widget, _alerts, _startup, new OnboardingViewModel(_localization, _settings));

        Assert.True(vm.StartWithWindows);
        Assert.True(vm.StartMinimized);
        Assert.Equal("fa-IR", vm.Language);
        Assert.False(vm.MinimizeToTray);
    }

    [Fact]
    public void EditingAStagedValue_MarksDirty()
    {
        Assert.False(_vm.IsDirty);

        _vm.StartWithWindows = true;

        Assert.True(_vm.IsDirty);
    }

    [Fact]
    public void Save_RegistersStartupWithMinimizedArgument()
    {
        _vm.StartWithWindows = true;
        _vm.StartMinimized = true;
        _vm.Save();

        Assert.Equal(1, _startup.EnableCalls);
        Assert.True(_startup.LastStartMinimized!.Value);
        Assert.Equal("True", _settings.Get(JsonSettingsService.StartWithWindowsKey, string.Empty));
        Assert.Equal("True", _settings.Get(JsonSettingsService.StartMinimizedKey, string.Empty));
        Assert.False(_vm.IsDirty);
        Assert.Equal("Saved", _vm.SavedNotice);
    }

    [Fact]
    public void Save_WithStartWithWindowsOff_RemovesRegistration()
    {
        _vm.Save();

        Assert.Equal(1, _startup.DisableCalls);
        Assert.Equal("False", _settings.Get(JsonSettingsService.StartWithWindowsKey, string.Empty));
    }

    [Fact]
    public void Save_DoesNotTouchUnknownKeys()
    {
        _settings.Set("someVendorOption", "keep-me");
        _vm.Save();

        Assert.Equal("keep-me", _settings.Get("someVendorOption", string.Empty));
    }

    [Fact]
    public void TrayToggles_ApplyImmediatelyWithoutSave()
    {
        _vm.MinimizeToTray = false;
        _vm.CloseToTray = false;

        Assert.Equal("False", _settings.Get("MinimizeToTray", string.Empty));
        Assert.Equal("False", _settings.Get("CloseToTray", string.Empty));
        Assert.True(_settings.SaveCalls >= 2);
    }

    [Fact]
    public void InvalidCooldown_BlocksSave()
    {
        _vm.CooldownText = "0";
        _vm.Save();

        Assert.NotEqual(string.Empty, _vm.ValidationError);
        Assert.Equal(string.Empty, _settings.Get("alerts.cooldownSeconds", string.Empty));
    }

    [Fact]
    public void OutOfRangeCooldown_BlocksSave()
    {
        _vm.CooldownText = "1441";
        _vm.Save();

        Assert.NotEqual(string.Empty, _vm.ValidationError);
    }

    [Fact]
    public void NonNumericThreshold_BlocksSave()
    {
        Rule("highDownloadSpeed").ThresholdText = "abc";
        _vm.Save();

        Assert.NotEqual(string.Empty, _vm.ValidationError);
    }

    [Fact]
    public void ZeroOrNegativeThreshold_BlocksSave()
    {
        Rule("highDownloadSpeed").ThresholdText = "0";
        _vm.Save();

        Assert.NotEqual(string.Empty, _vm.ValidationError);
    }

    [Fact]
    public void EnglishDecimal_AcceptedInPersianCulture()
    {
        _localization.SetCulture("fa-IR");
        _vm.CooldownText = "10";

        Rule("highDownloadSpeed").ThresholdText = "50.5";

        _vm.Save();

        Assert.Equal(string.Empty, _vm.ValidationError);
    }

    [Fact]
    public void Save_SpeedThreshold_ConvertsMbToBytesPerSecond()
    {
        var rule = Rule("highDownloadSpeed");
        rule.IsEnabled = true;
        rule.ThresholdText = "50";
        rule.UnitIndex = 1;

        _vm.Save();

        var stored = _settings.Get("alerts.highDownloadSpeed.threshold", string.Empty);
        Assert.Equal((50.0 * 1024 * 1024).ToString("0.##", CultureInfo.InvariantCulture), stored);
    }

    [Fact]
    public void Save_SizeThreshold_ConvertsGbToBytes()
    {
        var rule = Rule("dailyDownloadLimit");
        rule.IsEnabled = true;
        rule.ThresholdText = "10";
        rule.UnitIndex = 1;

        _vm.Save();

        var stored = _settings.Get("alerts.dailyDownloadLimit.threshold", string.Empty);
        Assert.Equal((10.0 * 1024 * 1024 * 1024).ToString("0.##", CultureInfo.InvariantCulture), stored);
    }

    [Fact]
    public void Save_Cooldown_StoresSeconds()
    {
        _vm.CooldownText = "90";
        _vm.Save();

        var stored = _settings.Get("alerts.cooldownSeconds", string.Empty);
        Assert.Equal((90 * 60).ToString("0.##", CultureInfo.InvariantCulture), stored);
        Assert.Equal(1, _alerts.RefreshCalls);
    }

    [Fact]
    public void Save_EnabledRule_PersistsEnabledFlag()
    {
        var rule = Rule("highUploadSpeed");
        rule.IsEnabled = true;

        _vm.Save();

        Assert.Equal("True", _settings.Get("alerts.highUploadSpeed.enabled", string.Empty));
    }

    [Fact]
    public void Save_WidgetEnabledChange_ShowsWidget()
    {
        _vm.WidgetEnabled = false;
        _vm.Save();
        _vm.WidgetEnabled = true;
        _vm.Save();

        Assert.True(_settings.Get("FloatingWidgetEnabled", string.Empty) == "True");
        Assert.True(_widget.ShowCalls >= 1);
    }

    [Fact]
    public void Save_AlwaysOnTopChange_SetsWidgetState()
    {
        _vm.WidgetAlwaysOnTop = true;
        _vm.Save();

        Assert.Equal("True", _settings.Get("FloatingWidgetAlwaysOnTop", string.Empty));
        Assert.True(_widget.IsAlwaysOnTop);

        _vm.WidgetAlwaysOnTop = false;
        _vm.Save();

        Assert.Equal("False", _settings.Get("FloatingWidgetAlwaysOnTop", string.Empty));
        Assert.True(_widget.SetAlwaysOnTopCalls >= 1);
        Assert.False(_widget.IsAlwaysOnTop);
    }

    [Fact]
    public void Save_AppliesChosenLanguage()
    {
        _vm.LanguageIndex = 1;
        _vm.Save();

        Assert.Equal("fa-IR", _settings.Get("language", string.Empty));
        Assert.Equal("fa-IR", _localization.CurrentCulture.Name);
    }

    [Fact]
    public void CultureChange_RelocalizesAlertNames()
    {
        _localization.SetCulture("fa-IR");

        Assert.Equal("سرعت دانلود بالا", Rule("highDownloadSpeed").Name);
        Assert.Equal("سرعت آپلود بالا", Rule("highUploadSpeed").Name);
    }

    [Fact]
    public void ResetToDefaults_StagesDefaultsAndMarksDirty()
    {
        _vm.StartWithWindows = true;
        _vm.StartMinimized = true;
        _vm.WidgetEnabled = true;
        _vm.CooldownText = "999";
        Rule("highDownloadSpeed").ThresholdText = "999";

        _vm.ResetToDefaults();

        Assert.False(_vm.StartWithWindows);
        Assert.False(_vm.StartMinimized);
        Assert.False(_vm.WidgetEnabled);
        Assert.Equal("5", _vm.CooldownText);
        Assert.False(Rule("highDownloadSpeed").IsEnabled);
        Assert.Equal("50", Rule("highDownloadSpeed").ThresholdText);
        Assert.True(_vm.IsDirty);
        Assert.True(_vm.WidgetAlwaysOnTop);
    }

    [Fact]
    public void ResetThenSave_PersistsDefaults()
    {
        _vm.StartWithWindows = true;
        _vm.ResetToDefaults();
        _vm.Save();

        Assert.Equal("False", _settings.Get(JsonSettingsService.StartWithWindowsKey, string.Empty));
        Assert.Equal(1, _startup.DisableCalls);
        Assert.Equal("300", _settings.Get("alerts.cooldownSeconds", string.Empty));
    }

    [Fact]
    public void GetStartedLabel_IsLocalized()
    {
        Assert.Equal("Get Started", _vm.GetStartedLabel);

        _localization.SetCulture("fa-IR");
        Assert.Equal("راهنمای شروع", _vm.GetStartedLabel);
    }

    [Fact]
    public void ShowGuideCommand_OpensOnboarding()
    {
        using var onboarding = new OnboardingViewModel(_localization, _settings);
        using var vm = new SettingsViewModel(_localization, _settings, _widget, _alerts, _startup, onboarding);
        Assert.False(onboarding.IsVisible);

        vm.ShowGuideCommand.Execute(null);

        Assert.True(onboarding.IsVisible);
    }

    [Fact]
    public void CultureSwitch_UpdatesAlertRuleNames()
    {
        var rule = Rule("highDownloadSpeed");
        Assert.Equal("High download speed", rule.Name);

        _localization.SetCulture("fa-IR");
        Assert.Equal("سرعت دانلود بالا", rule.Name);

        _localization.SetCulture("en-US");
        Assert.Equal("High download speed", rule.Name);
    }

    [Fact]
    public void CultureSwitch_RaisesAlertRuleNamePropertyChanged()
    {
        var rule = Rule("highDownloadSpeed");
        var changed = new List<string>();
        ((System.ComponentModel.INotifyPropertyChanged)rule).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AlertRuleViewModel.Name))
                changed.Add("Name");
        };

        _localization.SetCulture("fa-IR");
        Assert.Contains("Name", changed);
    }
}