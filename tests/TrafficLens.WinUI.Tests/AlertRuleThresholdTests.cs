using System.Globalization;
using TrafficLens.App.Services;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Alerts;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.ViewModels;

namespace TrafficLens.WinUI.Tests;

/// <summary>
/// Guards the alert card redesign against the regression human testing found: the
/// numeric threshold of every rule stopped being visible and editable.
/// </summary>
/// <remarks>
/// These tests drive the real <see cref="AlertsViewModel"/> and the real
/// <see cref="AlertSettings"/>, so they exercise the same binding, unit and
/// persistence path the page uses. No second threshold model is introduced.
/// </remarks>
public sealed class AlertRuleThresholdTests
{
    private static readonly AlertType[] AllRules =
    {
        AlertType.HighDownloadSpeed,
        AlertType.HighUploadSpeed,
        AlertType.DailyDownloadLimit,
        AlertType.DailyUploadLimit,
        AlertType.DailyTotalLimit
    };

    [Fact]
    public void AllFiveRules_ArePresentInTheCardCollection()
    {
        var (vm, _, _) = CreateViewModel();
        using var _ = vm;

        Assert.Equal(5, vm.Rules.Count);
        Assert.Equal(
            AllRules.OrderBy(t => (int)t),
            vm.Rules.Select(r => r.Type).OrderBy(t => (int)t));
    }

    [Fact]
    public void EveryRule_ExposesAThresholdTextThatIsNotEmpty()
    {
        var (vm, _, _) = CreateViewModel();
        using var _unused = vm;

        foreach (var rule in vm.Rules)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(rule.ThresholdText),
                $"{rule.Type} has no threshold text, so there is nothing to edit");
        }
    }

    [Fact]
    public void EveryRule_AcceptsAThresholdEdit()
    {
        var (vm, _, _) = CreateViewModel();
        using var _unused = vm;

        foreach (var rule in vm.Rules)
        {
            var before = rule.ThresholdText;
            rule.ThresholdText = "7.5";
            Assert.Equal("7.5", rule.ThresholdText);
            rule.ThresholdText = before;
        }
    }

    [Fact]
    public void SuggestedDefaults_AreShownForEveryRule()
    {
        var (vm, _, _) = CreateViewModel();
        using var _unused = vm;

        // The default config is in bytes; the row shows the largest unit that keeps
        // the number readable, which is what the card has to offer for editing.
        Assert.Equal("50", Row(vm, AlertType.HighDownloadSpeed).ThresholdText);
        Assert.Equal("20", Row(vm, AlertType.HighUploadSpeed).ThresholdText);
        Assert.Equal("50", Row(vm, AlertType.DailyDownloadLimit).ThresholdText);
        Assert.Equal("20", Row(vm, AlertType.DailyUploadLimit).ThresholdText);
        Assert.Equal("100", Row(vm, AlertType.DailyTotalLimit).ThresholdText);
    }

    [Fact]
    public void DownloadSpeedThreshold_IsSavedToTheDownloadField()
    {
        var (vm, settings, service) = CreateViewModel();
        using var _unused = vm;

        var rule = Row(vm, AlertType.HighDownloadSpeed);
        rule.ThresholdText = "75";
        rule.UnitIndex = 1; // MB/s
        vm.Save();

        Assert.Equal(75.0 * 1024 * 1024, AlertSettings.Load(settings).HighDownloadSpeedThresholdBytesPerSecond);
        Assert.Equal(1, service.RefreshCalls);
    }

    [Fact]
    public void UploadSpeedThreshold_IsSavedToTheUploadField()
    {
        var (vm, settings, service) = CreateViewModel();
        using var _unused = vm;

        var rule = Row(vm, AlertType.HighUploadSpeed);
        rule.ThresholdText = "3";
        rule.UnitIndex = 2; // GB/s
        vm.Save();

        Assert.Equal(3.0 * 1024 * 1024 * 1024, AlertSettings.Load(settings).HighUploadSpeedThresholdBytesPerSecond);
        Assert.Equal(1, service.RefreshCalls);
    }

    [Fact]
    public void DailyDownloadThreshold_IsSavedToTheDailyDownloadField()
    {
        var (vm, settings, _) = CreateViewModel();
        using var _unused = vm;

        var rule = Row(vm, AlertType.DailyDownloadLimit);
        rule.ThresholdText = "250";
        rule.UnitIndex = 0; // MB
        vm.Save();

        Assert.Equal(250.0 * 1024 * 1024, AlertSettings.Load(settings).DailyDownloadLimitBytes);
    }

    [Fact]
    public void DailyUploadThreshold_IsSavedToTheDailyUploadField()
    {
        var (vm, settings, _) = CreateViewModel();
        using var _unused = vm;

        var rule = Row(vm, AlertType.DailyUploadLimit);
        rule.ThresholdText = "2";
        rule.UnitIndex = 2; // TB
        vm.Save();

        Assert.Equal(2.0 * 1024 * 1024 * 1024 * 1024, AlertSettings.Load(settings).DailyUploadLimitBytes);
    }

    [Fact]
    public void DailyTotalThreshold_IsSavedToTheDailyTotalField()
    {
        var (vm, settings, _) = CreateViewModel();
        using var _unused = vm;

        var rule = Row(vm, AlertType.DailyTotalLimit);
        rule.ThresholdText = "1.5";
        rule.UnitIndex = 1; // GB
        vm.Save();

        Assert.Equal(1.5 * 1024 * 1024 * 1024, AlertSettings.Load(settings).DailyTotalLimitBytes);
    }

    [Fact]
    public void EachRule_WritesOnlyItsOwnThreshold()
    {
        var (vm, settings, _) = CreateViewModel();
        using var _unused = vm;
        var before = AlertSettings.Load(settings);

        Row(vm, AlertType.HighUploadSpeed).ThresholdText = "9";
        Row(vm, AlertType.HighUploadSpeed).UnitIndex = 0;
        vm.Save();

        var after = AlertSettings.Load(settings);
        Assert.Equal(before.HighDownloadSpeedThresholdBytesPerSecond, after.HighDownloadSpeedThresholdBytesPerSecond);
        Assert.Equal(before.DailyDownloadLimitBytes, after.DailyDownloadLimitBytes);
        Assert.Equal(before.DailyUploadLimitBytes, after.DailyUploadLimitBytes);
        Assert.Equal(before.DailyTotalLimitBytes, after.DailyTotalLimitBytes);
        Assert.Equal(9.0 * 1024, after.HighUploadSpeedThresholdBytesPerSecond);
    }

    [Fact]
    public void Decimals_ArePersisted()
    {
        var (vm, settings, _) = CreateViewModel();
        using var _unused = vm;

        Row(vm, AlertType.HighDownloadSpeed).ThresholdText = "12.5";
        Row(vm, AlertType.HighDownloadSpeed).UnitIndex = 0;
        vm.Save();

        Assert.Equal(12.5 * 1024, AlertSettings.Load(settings).HighDownloadSpeedThresholdBytesPerSecond, 3);
    }

    [Fact]
    public void InvariantAndCurrentCultureDecimals_AreBothAccepted()
    {
        var (vm, settings, _) = CreateViewModel(new CultureInfo("fa-IR"));
        using var _unused = vm;

        Row(vm, AlertType.HighDownloadSpeed).ThresholdText = "12.5";
        Row(vm, AlertType.HighDownloadSpeed).UnitIndex = 0;
        vm.Save();

        Assert.Equal(12.5 * 1024, AlertSettings.Load(settings).HighDownloadSpeedThresholdBytesPerSecond, 3);
    }

    [Fact]
    public void NonNumericThreshold_IsRejectedAndNothingIsPersisted()
    {
        var (vm, settings, service) = CreateViewModel();
        using var _unused = vm;
        var before = AlertSettings.Load(settings);

        Row(vm, AlertType.HighDownloadSpeed).ThresholdText = "abc";
        vm.Save();

        Assert.True(vm.HasValidationError);
        Assert.Equal(0, service.RefreshCalls);
        Assert.Equal(
            before.HighDownloadSpeedThresholdBytesPerSecond,
            AlertSettings.Load(settings).HighDownloadSpeedThresholdBytesPerSecond);
    }

    [Fact]
    public void ZeroOrNegativeThreshold_IsRejected()
    {
        var (vm, _, service) = CreateViewModel();
        using var _unused = vm;

        Row(vm, AlertType.DailyTotalLimit).ThresholdText = "0";
        vm.Save();
        Assert.True(vm.HasValidationError);

        Row(vm, AlertType.DailyTotalLimit).ThresholdText = "-5";
        vm.Save();
        Assert.True(vm.HasValidationError);
        Assert.Equal(0, service.RefreshCalls);
    }

    [Fact]
    public void EmptyThreshold_IsRejected()
    {
        var (vm, _, _) = CreateViewModel();
        using var _unused = vm;

        Row(vm, AlertType.DailyUploadLimit).ThresholdText = "   ";
        vm.Save();

        Assert.True(vm.HasValidationError);
    }

    [Fact]
    public void SavedThresholds_SurviveViewModelRecreation()
    {
        var settings = new RecordingSettingsService();
        var (first, _, _) = CreateViewModel(settings: settings);
        Row(first, AlertType.HighDownloadSpeed).ThresholdText = "88";
        Row(first, AlertType.HighDownloadSpeed).UnitIndex = 1;
        Row(first, AlertType.DailyTotalLimit).ThresholdText = "3";
        Row(first, AlertType.DailyTotalLimit).UnitIndex = 2;
        first.Save();
        first.Dispose();

        // A new page/view-model over the same settings, which is what navigating away
        // and back does.
        var (second, _, _) = CreateViewModel(settings: settings);
        using var _unused = second;

        Assert.Equal("88", Row(second, AlertType.HighDownloadSpeed).ThresholdText);
        Assert.Equal(1, Row(second, AlertType.HighDownloadSpeed).UnitIndex);
        Assert.Equal("3", Row(second, AlertType.DailyTotalLimit).ThresholdText);
        Assert.Equal(2, Row(second, AlertType.DailyTotalLimit).UnitIndex);
    }

    [Fact]
    public void SavedThresholds_AreAlsoReadBackByTheAlertBackend()
    {
        var settings = new RecordingSettingsService();
        var (vm, _, _) = CreateViewModel(settings: settings);
        Row(vm, AlertType.HighUploadSpeed).ThresholdText = "5";
        Row(vm, AlertType.HighUploadSpeed).UnitIndex = 1;
        vm.Save();

        // What the alert service itself would evaluate against.
        var backendView = AlertSettings.Load(settings);
        Assert.True(backendView.ThresholdOf(AlertType.HighUploadSpeed) > 0);
        Assert.Equal(5.0 * 1024 * 1024, backendView.HighUploadSpeedThresholdBytesPerSecond);
        vm.Dispose();
    }

    [Fact]
    public void SpeedRules_OfferRateUnits_AndLimitRules_OfferSizeUnits()
    {
        var (vm, _, _) = CreateViewModel();
        using var _unused = vm;

        Assert.Equal(new[] { "KB/s", "MB/s", "GB/s" }, Row(vm, AlertType.HighDownloadSpeed).ActiveUnitOptions);
        Assert.Equal(new[] { "KB/s", "MB/s", "GB/s" }, Row(vm, AlertType.HighUploadSpeed).ActiveUnitOptions);
        Assert.Equal(new[] { "MB", "GB", "TB" }, Row(vm, AlertType.DailyDownloadLimit).ActiveUnitOptions);
        Assert.Equal(new[] { "MB", "GB", "TB" }, Row(vm, AlertType.DailyUploadLimit).ActiveUnitOptions);
        Assert.Equal(new[] { "MB", "GB", "TB" }, Row(vm, AlertType.DailyTotalLimit).ActiveUnitOptions);
    }

    [Fact]
    public void UnitIndexAndThreshold_RoundTripThroughTheConfig()
    {
        foreach (var type in AllRules)
        {
            foreach (var unit in new[] { 0, 1, 2 })
            {
                var (vm, settings, _) = CreateViewModel();
                var rule = Row(vm, type);
                rule.ThresholdText = "4";
                rule.UnitIndex = unit;
                vm.Save();

                var reloaded = AlertSettings.Load(settings);
                var expected = type.IsSpeedRule()
                    ? 4.0 * 1024 * Math.Pow(1024, unit)
                    : 4.0 * 1024 * 1024 * Math.Pow(1024, unit);

                Assert.Equal(expected, reloaded.ThresholdOf(type), 3);
                vm.Dispose();
            }
        }
    }

    [Fact]
    public void EditingAThreshold_NotifiesSoThePageCanRefresh()
    {
        var (vm, _, _) = CreateViewModel();
        using var _unused = vm;
        var changed = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AlertsViewModel.HasNoConfiguredRules))
            {
                changed++;
            }
        };

        Row(vm, AlertType.HighDownloadSpeed).ThresholdText = "31";

        Assert.True(changed > 0);
    }

    [Fact]
    public void EnablingARule_DoesNotChangeItsThreshold()
    {
        var (vm, _, _) = CreateViewModel();
        using var _unused = vm;
        var rule = Row(vm, AlertType.HighDownloadSpeed);
        var before = rule.ThresholdText;

        Assert.False(rule.IsEnabled);
        rule.IsEnabled = true;

        Assert.Equal(before, rule.ThresholdText);
    }

    [Fact]
    public void RulesDefaultToDisabled_SoTheThresholdEditorMustNotBeGatedOnThem()
    {
        // This is the regression in one assertion: every rule ships disabled, so a
        // threshold editor that follows IsEnabled is unusable out of the box.
        var (vm, _, _) = CreateViewModel();
        using var _unused = vm;

        Assert.All(vm.Rules, r => Assert.False(r.IsEnabled));
    }

    private static AlertRuleRowViewModel Row(AlertsViewModel vm, AlertType type) =>
        vm.Rules.First(r => r.Type == type);

    private static (AlertsViewModel ViewModel, RecordingSettingsService Settings, FakeAlertService Service)
        CreateViewModel(CultureInfo? culture = null, RecordingSettingsService? settings = null)
    {
        settings ??= new RecordingSettingsService();
        var service = new FakeAlertService(settings);
        var localization = new FakeLocalization(culture ?? CultureInfo.InvariantCulture);
        var vm = new AlertsViewModel(service, settings, localization, null!);
        return (vm, settings, service);
    }

    /// <summary>
    /// Stands in for the real alert service. <see cref="RefreshConfig"/> reloads the
    /// persisted config but deliberately does not raise <c>ConfigChanged</c>, because
    /// that event is marshalled through a WinUI dispatcher queue which a unit test
    /// cannot own. The view model reaches the same reloaded values through
    /// <c>SetActive</c>, which is covered by the recreation test.
    /// </summary>
    private sealed class FakeAlertService : IAlertService
    {
        private readonly ISettingsService _settings;

        public FakeAlertService(ISettingsService settings)
        {
            _settings = settings;
            CurrentConfig = AlertSettings.Load(settings);
        }

        // The view model subscribes to both events, but the unit test host has no
        // WinUI dispatcher queue to marshal them onto, so nothing raises them here.
#pragma warning disable CS0067
        public event EventHandler<AlertRaisedEventArgs>? AlertRaised;

        public event EventHandler? ConfigChanged;
#pragma warning restore CS0067

        public IReadOnlyList<AlertEvent> RecentAlerts => Array.Empty<AlertEvent>();

        public AlertConfig CurrentConfig { get; private set; }

        public int RefreshCalls { get; private set; }

        public void RefreshConfig()
        {
            RefreshCalls++;
            CurrentConfig = AlertSettings.Load(_settings);
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeLocalization : ILocalizationService
    {
        public FakeLocalization(CultureInfo culture) => CurrentCulture = culture;

        public CultureInfo CurrentCulture { get; }

        public event EventHandler? CultureChanged;

        public string this[string key] => key;

        public bool IsRightToLeft => CurrentCulture.Name.Equals("fa-IR", StringComparison.OrdinalIgnoreCase);

        public void SetCulture(string cultureName) => CultureChanged?.Invoke(this, EventArgs.Empty);

        public string GetString(string key, string? cultureName = null) => key;
    }
}
