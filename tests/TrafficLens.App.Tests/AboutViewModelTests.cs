using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;

namespace TrafficLens.App.Tests;

public class AboutViewModelTests
{
    private static AboutViewModel CreateViewModel(string culture)
    {
        var localization = new LocalizationService();
        localization.SetCulture(culture);
        return new AboutViewModel(localization);
    }

    [Fact]
    public void EnglishCulture_PopulatesLocalizedLabelsAndInstanceTexts()
    {
        var vm = CreateViewModel("en-US");

        Assert.Equal("About", vm.AboutTitleLabel);
        Assert.Equal("TrafficLens", vm.ProductNameText);
        Assert.Equal("Version", vm.VersionLabel);
        Assert.Equal("Runtime", vm.RuntimeLabel);
        Assert.Equal("Operating system", vm.OsLabel);
        Assert.Equal("Language", vm.CultureLabel);
        Assert.Equal("Diagnostics", vm.DiagnosticsHeaderLabel);
        Assert.Equal("Copy diagnostics", vm.CopyDiagnosticsLabel);
        Assert.Equal("Open logs folder", vm.OpenLogFolderLabel);

        Assert.Equal("English (United States)", vm.CultureText);
        Assert.Contains("TrafficLens", vm.DataPathText);
        Assert.EndsWith("settings.json", vm.SettingsPathText);
        Assert.False(string.IsNullOrWhiteSpace(vm.VersionText));
        Assert.False(string.IsNullOrWhiteSpace(vm.RuntimeText));
        Assert.False(string.IsNullOrWhiteSpace(vm.OsText));
        Assert.False(string.IsNullOrWhiteSpace(vm.SettingsPathText));
        Assert.False(string.IsNullOrWhiteSpace(vm.LogsPathText));
    }

    [Fact]
    public void PersianCulture_PopulatesPersianLabels()
    {
        var vm = CreateViewModel("fa-IR");

        Assert.Equal("درباره", vm.AboutTitleLabel);
        Assert.Equal("نسخه", vm.VersionLabel);
        Assert.Equal("زمان اجرا", vm.RuntimeLabel);
        Assert.Equal("سیستم عامل", vm.OsLabel);
        Assert.Equal("زبان", vm.CultureLabel);
        Assert.Equal("اطلاعات تشخیص", vm.DiagnosticsHeaderLabel);
        Assert.Equal("کپی اطلاعات تشخیص", vm.CopyDiagnosticsLabel);
        Assert.Equal("باز کردن پوشه گزارش", vm.OpenLogFolderLabel);
        Assert.Equal("TrafficLens", vm.ProductNameText);
    }

    [Fact]
    public void CultureSwitch_RelocalizesLabelsAndCultureText()
    {
        var localization = new LocalizationService();
        localization.SetCulture("en-US");
        var vm = new AboutViewModel(localization);

        Assert.Equal("About", vm.AboutTitleLabel);

        localization.SetCulture("fa-IR");

        Assert.Equal("درباره", vm.AboutTitleLabel);
        Assert.Equal("نسخه", vm.VersionLabel);
        Assert.Equal("فارسی (ایران)", vm.CultureText);
    }

    [Fact]
    public void DiagnosticsInfo_BuildJoinsLabelValueLines()
    {
        var text = DiagnosticsInfo.Build(new[]
        {
            ("Product", "TrafficLens"),
            ("Version", "0.1.0"),
            ("Language", "English (United States)")
        });

        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Equal("Product: TrafficLens", lines[0]);
        Assert.Equal("Version: 0.1.0", lines[1]);
        Assert.Contains("Language: English (United States)", text);
    }

    [Fact]
    public void CopyDiagnosticsCommand_DoesNotThrow()
    {
        var vm = CreateViewModel("en-US");

        vm.CopyDiagnosticsCommand.Execute(null);

        Assert.False(string.IsNullOrWhiteSpace(vm.ProductNameText));
    }
}