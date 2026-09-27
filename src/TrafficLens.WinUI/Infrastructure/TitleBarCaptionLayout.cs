namespace TrafficLens.WinUI.Infrastructure;

/// <summary>
/// Turns the caption-button insets the shell reserves for the window into the
/// padding a custom title bar has to keep clear.
/// </summary>
/// <remarks>
/// The insets are reported in physical pixels by
/// <c>AppWindow.TitleBar.LeftInset</c> / <c>RightInset</c> and move to the other
/// edge when the window is right-to-left, so the safe area follows the real
/// caption-button geometry instead of a fixed or per-language margin. Nothing
/// here looks at the title text, which is what keeps a long Persian title and a
/// long English title on exactly the same rule.
/// </remarks>
public static class TitleBarCaptionLayout
{
    /// <summary>Leading breathing room between the window edge and the title text.</summary>
    public const double BasePaddingDip = 16.0;

    public const double BaseDpi = 96.0;

    /// <summary>
    /// Resolves the physical left/right padding (in DIPs) for the custom title bar.
    /// </summary>
    /// <param name="leftInsetPixels">
    /// Caption-button inset reported for the physical left edge. Non-zero when the
    /// shell has placed the caption buttons there, which is the right-to-left case.
    /// </param>
    /// <param name="rightInsetPixels">Caption-button inset for the physical right edge.</param>
    /// <param name="dpiScale">Window DPI divided by 96.</param>
    public static (double Left, double Right) ResolvePadding(
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
    public static bool HasCaptionArea(int leftInsetPixels, int rightInsetPixels) =>
        leftInsetPixels > 0 || rightInsetPixels > 0;
}
