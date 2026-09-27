namespace TrafficLens.WinUI.Services;

/// <summary>
/// Keeps the several Settings representations of the Floating Widget switch in
/// step with the single <see cref="WidgetEnabledState"/>.
/// </summary>
/// <remarks>
/// Settings shows the widget switch twice: a quick control near the top of the page
/// and the full widget section lower down (which also carries Always On Top). Both
/// are views of the same value - neither owns it - so a change to one, including a
/// change made by the widget's own close button, has to reach the other.
/// </remarks>
public static class WidgetToggleSync
{
    /// <summary>
    /// The state every widget switch must display.
    /// </summary>
    /// <param name="isEnabled">The persisted enabled flag.</param>
    /// <param name="isVisible">Whether the window is currently up.</param>
    public static bool Resolve(bool isEnabled, bool isVisible) => isEnabled || isVisible;

    /// <summary>
    /// Pushes one value into every widget switch, so the quick control and the
    /// full widget section can never disagree.
    /// </summary>
    public static void Apply(Action<bool> setQuickToggle, Action<bool> setSectionToggle, bool value)
    {
        setQuickToggle(value);
        setSectionToggle(value);
    }
}
