namespace TrafficLens.WinUI.Infrastructure;

/// <summary>
/// Turns the caption-button insets the shell reserves for the window into the
/// padding a custom title bar has to keep clear.
/// </summary>
/// <remarks>
/// <para>
/// <c>AppWindow.TitleBar.LeftInset</c> and <c>RightInset</c> are reported in
/// <b>flow order</b>, not as physical left and right: in a right-to-left window the
/// shell places the caption buttons on the leading edge, which is physically the
/// right, and still reports that inset as <c>LeftInset</c>. Measured on a Persian
/// window at 150% DPI, the buttons occupied the physical right (x 796..1003) while
/// <c>LeftInset</c> was the non-zero 207 and <c>RightInset</c> was 0.
/// </para>
/// <para>
/// Applying those values straight onto the physical sides therefore puts the safe
/// area on the wrong edge in right-to-left and lets the title run under Minimize /
/// Maximize / Close. <see cref="ResolvePadding"/> takes the insets in flow order and
/// the layout direction, and returns the padding for the physical left and right, so
/// the caller never has to know which way round the shell reported them. Nothing here
/// looks at the title text, which is what keeps a long Persian title and a long
/// English title on exactly the same rule.
/// </para>
/// </remarks>
public static class TitleBarCaptionLayout
{
    /// <summary>Leading breathing room between the window edge and the title text.</summary>
    public const double BasePaddingDip = 16.0;

    public const double BaseDpi = 96.0;

    /// <summary>
    /// Resolves the physical left/right padding (in DIPs) for the custom title bar.
    /// </summary>
    /// <param name="leadingInsetPixels">
    /// Caption-button inset the shell reported for the leading edge: the physical
    /// left in a left-to-right window, the physical right in a right-to-left one.
    /// </param>
    /// <param name="trailingInsetPixels">The inset for the opposite, trailing edge.</param>
    /// <param name="dpiScale">Window DPI divided by 96.</param>
    /// <param name="isRightToLeft">
    /// Whether the window lays out right-to-left, which is what decides whether the
    /// leading inset belongs on the physical right.
    /// </param>
    public static (double Left, double Right) ResolvePadding(
        int leadingInsetPixels,
        int trailingInsetPixels,
        double dpiScale,
        bool isRightToLeft)
    {
        var scale = dpiScale > 0 ? dpiScale : 1.0;
        var leading = BasePaddingDip + Math.Max(0, leadingInsetPixels) / scale;
        var trailing = BasePaddingDip + Math.Max(0, trailingInsetPixels) / scale;

        return isRightToLeft ? (trailing, leading) : (leading, trailing);
    }

    /// <summary>
    /// Picks the caption safe area to apply, preferring what the shell reports and
    /// falling back to the controls measured on the window itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A reported inset is not proof of a reserved caption area. The shell has been
    /// observed reporting no inset at all for a window that still draws the three
    /// controls, and reporting only the resize frame, which reserves a strip far
    /// narrower than the controls. Either way the title ends up underneath them, so
    /// the insets are used only when they actually cover the controls that were found.
    /// </para>
    /// <para>
    /// A measured reserve is larger than the shell's whenever the shell reports the
    /// frame alone, because the measurement runs from the inner edge of the controls to
    /// the window edge, so preferring the larger of the two can only ever reserve more,
    /// never less. The shell path stays in charge whenever it is sufficient, and stays
    /// in charge outright when nothing could be measured.
    /// </para>
    /// </remarks>
    /// <param name="leadingInsetPixels">Leading shell inset, in flow order.</param>
    /// <param name="trailingInsetPixels">Trailing shell inset, in flow order.</param>
    /// <param name="measuredLeftInsetPixels">Measured physical left inset, or null when the controls could not be located.</param>
    /// <param name="measuredRightInsetPixels">Measured physical right inset, or null when the controls could not be located.</param>
    /// <param name="dpiScale">Window DPI divided by 96.</param>
    /// <param name="isRightToLeft">Whether the window lays out right-to-left.</param>
    /// <returns>
    /// The physical left/right padding to apply, and whether it came from the measured
    /// controls. A measured padding is already in physical sides and must be applied as
    /// it stands; a shell padding is in flow order and still needs the caller to map it
    /// with the layout direction.
    /// </returns>
    public static ((double Left, double Right) Padding, bool UsedMeasuredControls) Resolve(
        int leadingInsetPixels,
        int trailingInsetPixels,
        int? measuredLeftInsetPixels,
        int? measuredRightInsetPixels,
        double dpiScale,
        bool isRightToLeft)
    {
        var shell = ResolvePadding(leadingInsetPixels, trailingInsetPixels, dpiScale, isRightToLeft);

        if (measuredLeftInsetPixels is null || measuredRightInsetPixels is null)
        {
            return (shell, false);
        }

        var measured = ResolvePhysicalInsets(measuredLeftInsetPixels.Value, measuredRightInsetPixels.Value, dpiScale);

        // ResolvePadding already returned physical sides, having mapped the flow-order
        // insets itself, so the two reserves are compared edge for edge.
        return shell.Left >= measured.Left && shell.Right >= measured.Right
            ? (shell, false)
            : (measured, true);
    }

    /// <summary>
    /// Resolves the same physical left/right padding from insets that are already
    /// known to be physical sides rather than flow order.
    /// </summary>
    /// <remarks>
    /// Used for the fallback path, where the caption region is read back from the
    /// window itself. That measurement is in window coordinates, so it already
    /// carries the physical side and must not be swapped again for a right-to-left
    /// window: the value returned for a right side stays on the right.
    /// </remarks>
    /// <param name="leftInsetPixels">Reserved area on the physical left, in pixels.</param>
    /// <param name="rightInsetPixels">Reserved area on the physical right, in pixels.</param>
    /// <param name="dpiScale">Window DPI divided by 96.</param>
    public static (double Left, double Right) ResolvePhysicalInsets(
        int leftInsetPixels,
        int rightInsetPixels,
        double dpiScale)
    {
        var scale = dpiScale > 0 ? dpiScale : 1.0;

        return (
            BasePaddingDip + Math.Max(0, leftInsetPixels) / scale,
            BasePaddingDip + Math.Max(0, rightInsetPixels) / scale);
    }

    /// <summary>
    /// Reads the current window DPI as a scale factor. Exposed so the title bar
    /// converts insets with the same scale factor the window was sized with.
    /// </summary>
    public static double ScaleFromDpi(uint dpi) =>
        dpi == 0 ? 1.0 : Math.Max(1.0, dpi / BaseDpi);

    /// <summary>True when either edge has a caption-button area that must stay clear.</summary>
    public static bool HasCaptionArea(int leadingInsetPixels, int trailingInsetPixels) =>
        leadingInsetPixels > 0 || trailingInsetPixels > 0;
}
