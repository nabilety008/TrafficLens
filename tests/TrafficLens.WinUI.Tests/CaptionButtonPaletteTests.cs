using Microsoft.UI;
using TrafficLens.WinUI.Infrastructure;

namespace TrafficLens.WinUI.Tests;

/// <summary>
/// The native caption buttons must be readable in both themes and both activation
/// states. The palette is the single decision point, so the four combinations are
/// pinned here: a dark window keeps light glyphs (the regression this batch fixes
/// was black glyphs on a dark title bar), a light window keeps dark glyphs, and an
/// inactive window dims the resting tone without changing hover/press structure.
/// Backgrounds stay transparent on every path so the window's own title-bar area
/// shows through and no state paints a visible plate while resting.
/// </summary>
public class CaptionButtonPaletteTests
{
    [Fact]
    public void DarkActive_GlyphsAreWhiteWithTransparentBackground()
    {
        var c = CaptionButtonPalette.Resolve(isDarkTheme: true, isActive: true);

        Assert.Equal(Colors.White, c.Foreground);
        Assert.Equal(Colors.Transparent, c.Background);
        Assert.Equal(Colors.Transparent, c.InactiveBackground);
    }

    [Fact]
    public void DarkInactive_GlyphsAreDimmedButNotBlack()
    {
        var c = CaptionButtonPalette.Resolve(isDarkTheme: true, isActive: false);

        // The regression this prevents: an unfocused dark window must not fall
        // back to near-black glyphs on the dark title bar.
        Assert.NotEqual(Colors.Black, c.Foreground);
        Assert.True(c.Foreground.R > 0x80, "inactive dark glyphs must stay light");
        Assert.Equal(Colors.Transparent, c.Background);
    }

    [Fact]
    public void LightActive_GlyphsAreDarkWithTransparentBackground()
    {
        var c = CaptionButtonPalette.Resolve(isDarkTheme: false, isActive: true);

        Assert.True(c.Foreground.R < 0x40, "light-theme resting glyphs must be dark");
        Assert.Equal(Colors.Transparent, c.Background);
    }

    [Fact]
    public void LightInactive_GlyphsAreDimmedButReadable()
    {
        var c = CaptionButtonPalette.Resolve(isDarkTheme: false, isActive: false);

        Assert.NotEqual(Colors.Black, c.Foreground);
        Assert.NotEqual(Colors.White, c.Foreground);
        Assert.Equal(Colors.Transparent, c.Background);
    }

    [Fact]
    public void HoverAndPressed_AreStrongerThanRestingOnBothThemes()
    {
        foreach (var dark in new[] { true, false })
        {
            var c = CaptionButtonPalette.Resolve(dark, isActive: true);

            // Pressed is a visibly stronger plate than hover, so the three
            // states stay distinguishable; both are non-opaque overlays.
            Assert.NotEqual(c.HoverBackground, c.PressedBackground);
            Assert.NotEqual((byte)0x00, c.PressedBackground.A);
            Assert.NotEqual((byte)0xFF, c.HoverBackground.A);

            // Hover/pressed keep the full-strength resting glyph tone.
            Assert.Equal(c.Foreground, c.HoverForeground);
            Assert.Equal(c.Foreground, c.PressedForeground);
        }
    }

    [Fact]
    public void InactiveForeground_MatchesResolvedInactiveTone()
    {
        var darkInactive = CaptionButtonPalette.Resolve(true, isActive: false);
        Assert.Equal(darkInactive.Foreground, darkInactive.InactiveForeground);

        var lightInactive = CaptionButtonPalette.Resolve(false, isActive: false);
        Assert.Equal(lightInactive.Foreground, lightInactive.InactiveForeground);

        // An active window still carries a defined inactive tone for the
        // shell's own inactive-state transitions.
        var darkActive = CaptionButtonPalette.Resolve(true, isActive: true);
        Assert.NotEqual(darkActive.Foreground, darkActive.InactiveForeground);
    }
}
