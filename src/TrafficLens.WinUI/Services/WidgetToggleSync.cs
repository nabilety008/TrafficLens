namespace TrafficLens.WinUI.Services;

/// <summary>
/// Keeps the Floating Widget switches in step with the single
/// <see cref="WidgetEnabledState"/> they are all views of.
/// </summary>
/// <remarks>
/// There are two switches and they live in different windows: the quick action in the
/// MainWindow title bar, for immediate access from anywhere, and the enable switch in
/// the Settings widget section, which also carries Always On Top. Neither owns the
/// value, so a change to one - including a change made by the widget's own close
/// button - has to reach the other.
/// </remarks>
public static class WidgetToggleSync
{
    /// <summary>
    /// The state every widget switch must display.
    /// </summary>
    /// <param name="isEnabled">The persisted enabled flag.</param>
    /// <param name="isVisible">Whether the window is currently up.</param>
    public static bool Resolve(bool isEnabled, bool isVisible) => isEnabled || isVisible;
}
