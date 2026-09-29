namespace TrafficLens.WinUI.Infrastructure;

using Microsoft.UI;

/// <summary>
/// Caption-button colors for the real Windows caption buttons over the custom
/// title bar, resolved for the window's current theme and activation state.
/// </summary>
/// <remarks>
/// <para>
/// <c>ExtendsContentIntoTitleBar</c> draws the app's own background behind the
/// caption controls, so the system default button colors stop matching: in a dark
/// window the glyphs can render black on the dark title bar and become unreadable.
/// <see cref="AppWindow.TitleBar"/> exposes per-state button colors for exactly
/// this case, and this type is the single place that decides them.
/// </para>
/// <para>
/// The rule is simple and theme-derived: the buttons get the same background as
/// the title bar (transparent), the same foreground as the title text, a hover
/// state one step stronger than the resting one, and Windows' own close-button
/// treatment is left to the system by keeping the close colors unset where the
/// caller can. Nothing here reads a control or a window, so it is pure and
/// directly unit-testable for all four theme × activation combinations.
/// </para>
/// </remarks>
public static class CaptionButtonPalette
{
    /// <summary>Foreground of the title text, matching the resting glyph tone.</summary>
    public const byte RestingGlyphDark = 0xFF;   // white glyph on a dark title bar
    public const byte RestingGlyphLight = 0x19;  // near-black glyph on a light title bar

    /// <summary>
    /// Resolves the full caption-button color set for one theme and activation
    /// state. Backgrounds are transparent so the window's own title-bar area
    /// shows through unchanged; the foreground follows the theme the way the
    /// title text does, and hover/pressed step one shade stronger.
    /// </summary>
    /// <param name="isDarkTheme">The window's effective theme (dark or light).</param>
    /// <param name="isActive">Whether the window is the focused/active window.</param>
    public static CaptionButtonColors Resolve(bool isDarkTheme, bool isActive)
    {
        // Resting glyph: the tone the title text uses in this theme. An inactive
        // window dims it, exactly like the title text loses emphasis when the
        // window loses focus.
        var resting = isDarkTheme
            ? Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)
            : Windows.UI.Color.FromArgb(0xFF, 0x19, 0x19, 0x19);        var inactive = isDarkTheme
            ? Windows.UI.Color.FromArgb(0xFF, 0x8A, 0x8A, 0x8A)
            : Windows.UI.Color.FromArgb(0xFF, 0x8A, 0x8A, 0x8A);

        var foreground = isActive ? resting : inactive;

        // Hover/pressed: one step stronger than the resting tone, on both themes.
        // In dark theme the hover background lightens slightly (a lighter plate
        // under a white glyph); in light theme it darkens slightly. Pressed goes
        // one more step, so the three states stay clearly distinguishable without
        // inventing a non-Windows-like color.
        var hoverBackground = isDarkTheme
            ? Windows.UI.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)
            : Windows.UI.Color.FromArgb(0x14, 0x00, 0x00, 0x00);
        var pressedBackground = isDarkTheme
            ? Windows.UI.Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF)
            : Windows.UI.Color.FromArgb(0x2A, 0x00, 0x00, 0x00);

        var hoverForeground = resting;
        var pressedForeground = resting;

        return new CaptionButtonColors(
            Foreground: foreground,
            Background: Colors.Transparent,
            HoverForeground: hoverForeground,
            HoverBackground: hoverBackground,
            PressedForeground: pressedForeground,
            PressedBackground: pressedBackground,
            InactiveForeground: inactive,
            InactiveBackground: Colors.Transparent);
    }
}

/// <summary>
/// One resolved caption-button color set. Every member maps 1:1 onto an
/// <c>AppWindow.TitleBar</c> button color property.
/// </summary>
public readonly record struct CaptionButtonColors(
    Windows.UI.Color Foreground,
    Windows.UI.Color Background,
    Windows.UI.Color HoverForeground,
    Windows.UI.Color HoverBackground,
    Windows.UI.Color PressedForeground,
    Windows.UI.Color PressedBackground,
    Windows.UI.Color InactiveForeground,
    Windows.UI.Color InactiveBackground);
