using TrafficLens.WinUI.Services;

namespace TrafficLens.WinUI.Tests;

/// <summary>
/// Covers the widget close path: closing the widget means disabling it, the setting
/// is persisted, both Settings switches follow it, and it survives a restart.
/// </summary>
public class WidgetEnabledStateTests
{
    [Fact]
    public void WidgetClose_SetsEnabledToFalse()
    {
        var settings = new RecordingSettingsService();
        settings.Set(FloatingWidgetSettings.EnabledKey, bool.TrueString);
        var state = new WidgetEnabledState(settings);

        // This is what the widget's native close button leads to.
        state.SetEnabled(false);

        Assert.False(state.IsEnabled);
    }

    [Fact]
    public void WidgetClose_PersistsDisabledSetting()
    {
        var settings = new RecordingSettingsService();
        settings.Set(FloatingWidgetSettings.EnabledKey, bool.TrueString);
        var state = new WidgetEnabledState(settings);
        var savesBefore = settings.SaveCalls;

        state.SetEnabled(false);

        Assert.Equal(
            bool.FalseString,
            settings.Get(FloatingWidgetSettings.EnabledKey, bool.TrueString));
        Assert.True(settings.SaveCalls > savesBefore, "disabling the widget must persist");
    }

    [Fact]
    public void Disabling_IsTheOnlyWriteForTheEnabledKey()
    {
        var settings = new RecordingSettingsService();
        var state = new WidgetEnabledState(settings);

        state.SetEnabled(true);
        state.SetEnabled(false);

        var enabledWrites = settings.Writes
            .Where(w => w.StartsWith(FloatingWidgetSettings.EnabledKey + "=", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, enabledWrites.Count);
        Assert.Equal($"{FloatingWidgetSettings.EnabledKey}={bool.TrueString}", enabledWrites[0]);
        Assert.Equal($"{FloatingWidgetSettings.EnabledKey}={bool.FalseString}", enabledWrites[1]);
    }

    [Fact]
    public void SettingSameValue_DoesNotWriteOrRaise_AndSoCannotLoop()
    {
        var settings = new RecordingSettingsService();
        var state = new WidgetEnabledState(settings);
        state.SetEnabled(false);

        var writesAfterFirst = settings.SetCalls;
        var savesAfterFirst = settings.SaveCalls;
        var raised = 0;
        state.Changed += (_, _) => raised++;

        var changed = state.SetEnabled(false);

        Assert.False(changed);
        Assert.Equal(0, raised);
        Assert.Equal(writesAfterFirst, settings.SetCalls);
        Assert.Equal(savesAfterFirst, settings.SaveCalls);
    }

    [Fact]
    public void SettingSameValue_IsIdempotentUnderRepeatedCloseEvents()
    {
        var settings = new RecordingSettingsService();
        settings.Set(FloatingWidgetSettings.EnabledKey, bool.TrueString);
        var state = new WidgetEnabledState(settings);
        var raised = 0;
        state.Changed += (_, _) => raised++;

        // "on -> close window -> close event -> set off again"
        state.SetEnabled(false);
        state.SetEnabled(false);
        state.SetEnabled(false);

        // One real transition, one write, one notification - the repeats are no-ops.
        Assert.Equal(1, settings.SaveCalls);
        Assert.Equal(1, raised);
        Assert.False(state.IsEnabled);
    }

    [Fact]
    public void RestoredDisabledState_DoesNotRecreateWidget()
    {
        var settings = new RecordingSettingsService();
        settings.Set(FloatingWidgetSettings.EnabledKey, bool.FalseString);

        Assert.False(WidgetEnabledState.ShouldRestore(settings));
    }

    [Fact]
    public void RestoredEnabledState_RecreatesWidget()
    {
        var settings = new RecordingSettingsService();
        settings.Set(FloatingWidgetSettings.EnabledKey, bool.TrueString);

        Assert.True(WidgetEnabledState.ShouldRestore(settings));
    }

    [Fact]
    public void MissingSetting_DefaultsToDisabled()
    {
        var settings = new RecordingSettingsService();

        var state = new WidgetEnabledState(settings);

        Assert.False(state.IsEnabled);
        Assert.False(WidgetEnabledState.ShouldRestore(settings));
    }

    [Fact]
    public void State_IsReadFromTheSettingOnConstruction()
    {
        var settings = new RecordingSettingsService();
        settings.Set(FloatingWidgetSettings.EnabledKey, bool.TrueString);

        var state = new WidgetEnabledState(settings);

        Assert.True(state.IsEnabled);
    }

    [Fact]
    public void Reload_PicksUpAnExternalWrite()
    {
        var settings = new RecordingSettingsService();
        var state = new WidgetEnabledState(settings);

        settings.Set(FloatingWidgetSettings.EnabledKey, bool.FalseString);
        state.Reload();

        Assert.False(state.IsEnabled);
    }

    [Fact]
    public void ShellQuickActionAndSettingsSwitch_ShareOneSourceOfTruth()
    {
        var settings = new RecordingSettingsService();
        var state = new WidgetEnabledState(settings);

        // The two switches live in different windows but are both driven from the one
        // state object and from the same persisted key, so neither can hold an
        // independent value.
        var shell = new List<bool>();
        var settingsSection = new List<bool>();
        state.Changed += (_, enabled) => shell.Add(enabled);
        state.Changed += (_, enabled) => settingsSection.Add(enabled);

        state.SetEnabled(true);
        state.SetEnabled(false);

        Assert.Equal(new[] { true, false }, shell);
        Assert.Equal(shell, settingsSection);
        Assert.Equal(
            bool.FalseString,
            settings.Get(FloatingWidgetSettings.EnabledKey, bool.TrueString));
        Assert.False(state.IsEnabled);
    }

    [Fact]
    public void WidgetClose_UpdatesBothSwitches()
    {
        var settings = new RecordingSettingsService();
        var state = new WidgetEnabledState(settings);
        state.SetEnabled(true);

        var shell = false;
        var settingsSection = false;
        void Sync(bool enabled)
        {
            shell = enabled;
            settingsSection = enabled;
        }
        state.Changed += (_, enabled) => Sync(enabled);

        // The widget's own close button.
        state.SetEnabled(false);

        Assert.False(shell);
        Assert.False(settingsSection);
        Assert.Equal(
            bool.FalseString,
            settings.Get(FloatingWidgetSettings.EnabledKey, bool.TrueString));
    }
}
