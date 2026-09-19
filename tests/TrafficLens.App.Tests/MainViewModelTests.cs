using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;

namespace TrafficLens.App.Tests;

public class MainViewModelTests
{
    private static MainViewModel CreateViewModel(string culture)
    {
        var localization = new LocalizationService();
        localization.SetCulture(culture);

        var settings = new FakeSettingsService();
        var floatingWidget = new FakeFloatingWidgetService();

        var dashboard = new DashboardViewModel(new FakeCollector(), new FakeAdapterProvider(), localization);
        var applications = new ApplicationsViewModel(new FakeProcessCollector(), localization, new ProcessIconResolver());
        var connections = new ConnectionsViewModel(new FakeConnectionProvider(), localization, new ProcessIconResolver());
        var history = new HistoryViewModel(new FakeHistoryService(), localization);
        var alerts = new AlertsViewModel(new FakeAlertService(), localization);
        var onboarding = new OnboardingViewModel(localization, settings);
        var settingsPage = new SettingsViewModel(localization, settings, floatingWidget, new FakeAlertService(), new FakeStartupRegistrationService(), onboarding);
        var about = new AboutViewModel(localization, onboarding);

        return new MainViewModel(
            localization,
            settings,
            dashboard,
            applications,
            connections,
            history,
            alerts,
            settingsPage,
            about,
            floatingWidget,
            onboarding);
    }

    [Fact]
    public void ShowAboutCommand_ShowsOnlyAboutPage()
    {
        using var vm = CreateViewModel("en-US");

        vm.ShowAboutCommand.Execute(null);

        Assert.True(vm.IsAboutVisible);
        Assert.False(vm.IsDashboardVisible);
        Assert.False(vm.IsApplicationsVisible);
        Assert.False(vm.IsConnectionsVisible);
        Assert.False(vm.IsHistoryVisible);
        Assert.False(vm.IsAlertsVisible);
        Assert.False(vm.IsSettingsVisible);
        Assert.Equal("About", vm.AboutNavLabel);
    }

    [Fact]
    public void NavigateAwayFromAbout_ReturnsToDashboard()
    {
        using var vm = CreateViewModel("en-US");

        vm.ShowAboutCommand.Execute(null);
        Assert.True(vm.IsAboutVisible);

        vm.ShowDashboardCommand.Execute(null);

        Assert.True(vm.IsDashboardVisible);
        Assert.False(vm.IsAboutVisible);
    }

    [Fact]
    public void PersianCulture_LocalizesAboutNavLabel()
    {
        using var vm = CreateViewModel("fa-IR");

        Assert.Equal("درباره", vm.AboutNavLabel);
    }

    [Fact]
    public void CultureSwitch_RelocalizesAboutNavLabel()
    {
        var localization = new LocalizationService();
        localization.SetCulture("en-US");

        var settings = new FakeSettingsService();
        var floatingWidget = new FakeFloatingWidgetService();
        var onboarding = new OnboardingViewModel(localization, settings);

        using var vm = new MainViewModel(
            localization,
            settings,
            new DashboardViewModel(new FakeCollector(), new FakeAdapterProvider(), localization),
            new ApplicationsViewModel(new FakeProcessCollector(), localization, new ProcessIconResolver()),
            new ConnectionsViewModel(new FakeConnectionProvider(), localization, new ProcessIconResolver()),
            new HistoryViewModel(new FakeHistoryService(), localization),
            new AlertsViewModel(new FakeAlertService(), localization),
            new SettingsViewModel(localization, settings, floatingWidget, new FakeAlertService(), new FakeStartupRegistrationService(), onboarding),
            new AboutViewModel(localization, onboarding),
            floatingWidget,
            onboarding);

        Assert.Equal("About", vm.AboutNavLabel);

        localization.SetCulture("fa-IR");

        Assert.Equal("درباره", vm.AboutNavLabel);
    }

    [Fact]
    public void ShowAboutCommand_PopulatesAboutPageData()
    {
        using var vm = CreateViewModel("en-US");

        vm.ShowAboutCommand.Execute(null);

        Assert.Equal("TrafficLens", vm.About.ProductNameText);
        Assert.False(string.IsNullOrWhiteSpace(vm.About.VersionText));
        Assert.Equal("About", vm.About.AboutTitleLabel);
    }

    [Fact]
    public void FreshProfile_OnboardingIsShown()
    {
        var localization = new LocalizationService();
        localization.SetCulture("en-US");
        var settings = new FakeSettingsService { SettingsFileExisted = false };
        var floatingWidget = new FakeFloatingWidgetService();
        var onboarding = new OnboardingViewModel(localization, settings);

        using var vm = new MainViewModel(
            localization,
            settings,
            new DashboardViewModel(new FakeCollector(), new FakeAdapterProvider(), localization),
            new ApplicationsViewModel(new FakeProcessCollector(), localization, new ProcessIconResolver()),
            new ConnectionsViewModel(new FakeConnectionProvider(), localization, new ProcessIconResolver()),
            new HistoryViewModel(new FakeHistoryService(), localization),
            new AlertsViewModel(new FakeAlertService(), localization),
            new SettingsViewModel(localization, settings, floatingWidget, new FakeAlertService(), new FakeStartupRegistrationService(), onboarding),
            new AboutViewModel(localization, onboarding),
            floatingWidget,
            onboarding);

        Assert.True(vm.Onboarding.IsVisible);
    }

    [Fact]
    public void ExistingProfile_OnboardingIsNotAutoShown()
    {
        using var vm = CreateViewModel("en-US");

        Assert.False(vm.Onboarding.IsVisible);
    }
}