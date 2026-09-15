using TrafficLens.App.Services;

namespace TrafficLens.App.Tests;

public sealed class TrayBehaviorTests
{
    [Fact]
    public void GetMinimizeToTray_WithNoSetting_DefaultsToTrue()
    {
        var settings = new FakeSettingsService();

        Assert.True(TrayBehavior.GetMinimizeToTray(settings));
    }

    [Fact]
    public void GetMinimizeToTray_WithExplicitFalse_ReturnsFalse()
    {
        var settings = new FakeSettingsService();
        settings.Set(TrayBehavior.MinimizeToTrayKey, bool.FalseString);

        Assert.False(TrayBehavior.GetMinimizeToTray(settings));
    }

    [Fact]
    public void GetCloseToTray_WithNoSetting_DefaultsToTrue()
    {
        var settings = new FakeSettingsService();

        Assert.True(TrayBehavior.GetCloseToTray(settings));
    }

    [Fact]
    public void GetCloseToTray_WithExplicitFalse_ReturnsFalse()
    {
        var settings = new FakeSettingsService();
        settings.Set(TrayBehavior.CloseToTrayKey, bool.FalseString);

        Assert.False(TrayBehavior.GetCloseToTray(settings));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolveCloseAction_WhenExitRequested_AlwaysExits(bool closeToTray)
    {
        var settings = new FakeSettingsService();
        settings.Set(TrayBehavior.CloseToTrayKey, closeToTray.ToString());

        Assert.Equal(WindowCloseAction.Exit, TrayBehavior.ResolveCloseAction(isExitRequested: true, settings));
    }

    [Fact]
    public void ResolveCloseAction_WhenCloseToTrayEnabled_AndNotExiting_HidesToTray()
    {
        var settings = new FakeSettingsService();
        settings.Set(TrayBehavior.CloseToTrayKey, bool.TrueString);

        Assert.Equal(WindowCloseAction.HideToTray, TrayBehavior.ResolveCloseAction(isExitRequested: false, settings));
    }

    [Fact]
    public void ResolveCloseAction_WhenCloseToTrayDisabled_AndNotExiting_Exits()
    {
        var settings = new FakeSettingsService();
        settings.Set(TrayBehavior.CloseToTrayKey, bool.FalseString);

        Assert.Equal(WindowCloseAction.Exit, TrayBehavior.ResolveCloseAction(isExitRequested: false, settings));
    }

    [Fact]
    public void CloseNotice_StartsUnshown_ThenPersistsAfterBeingMarked()
    {
        var settings = new FakeSettingsService();

        Assert.True(TrayBehavior.ShouldShowFirstCloseNotice(settings));

        TrayBehavior.MarkCloseNoticeShown(settings);

        Assert.False(TrayBehavior.ShouldShowFirstCloseNotice(settings));

        var reloaded = new FakeSettingsService();
        reloaded.Set(TrayBehavior.TrayCloseNoticeShownKey, settings.Get(TrayBehavior.TrayCloseNoticeShownKey, string.Empty));

        Assert.False(TrayBehavior.ShouldShowFirstCloseNotice(reloaded));
    }

    [Fact]
    public void SetTrayOptions_PersistThroughSettingsService()
    {
        var settings = new FakeSettingsService();

        settings.Set(TrayBehavior.MinimizeToTrayKey, bool.FalseString);
        settings.Set(TrayBehavior.CloseToTrayKey, bool.FalseString);

        Assert.False(TrayBehavior.GetMinimizeToTray(settings));
        Assert.False(TrayBehavior.GetCloseToTray(settings));
    }
}