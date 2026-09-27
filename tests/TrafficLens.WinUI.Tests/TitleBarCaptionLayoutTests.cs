using TrafficLens.WinUI.Infrastructure;

namespace TrafficLens.WinUI.Tests;

/// <summary>
/// The title bar keeps clear of the caption buttons because it follows the caption
/// geometry the shell reports, not because of a margin chosen for a given string.
/// </summary>
public class TitleBarCaptionLayoutTests
{
    private const double Base = TitleBarCaptionLayout.BasePaddingDip;

    [Fact]
    public void Layout_DoesNotDependOnTheTitleText()
    {
        // The helper takes only caption geometry, scale and layout direction. There
        // is no string, and therefore no language, a per-language offset could hide in.
        var parameters = typeof(TitleBarCaptionLayout)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name == nameof(TitleBarCaptionLayout.ResolvePadding))
            .SelectMany(m => m.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        Assert.Equal(
            new[] { typeof(int), typeof(int), typeof(double), typeof(bool) },
            parameters);
        Assert.DoesNotContain(typeof(string), parameters);
    }

    [Fact]
    public void EnglishLtr_CaptionButtonsOnTheLeadingLeftEdge()
    {
        var (left, right) = TitleBarCaptionLayout.ResolvePadding(
            leadingInsetPixels: 138,
            trailingInsetPixels: 0,
            dpiScale: 1.0,
            isRightToLeft: false);

        Assert.Equal(Base + 138, left);
        Assert.Equal(Base, right);
    }

    [Fact]
    public void PersianRtl_CaptionButtonsOnTheLeadingRightEdge()
    {
        // Measured on the real Persian window at 150% DPI: the shell reported the
        // non-zero inset as the leading one (LeftInset = 207 px) while the caption
        // buttons were physically on the right at x 796..1003. The safe area has to
        // follow the buttons onto the physical right, not the physical left.
        var (left, right) = TitleBarCaptionLayout.ResolvePadding(
            leadingInsetPixels: 207,
            trailingInsetPixels: 0,
            dpiScale: 1.5,
            isRightToLeft: true);

        Assert.Equal(Base, left);
        Assert.Equal(Base + 138, right);
    }

    [Fact]
    public void PersianRtl_KeepsTheTitleClearOfTheCloseButton()
    {
        // Regression guard for the measured defect: with the sides flipped wrongly
        // the padding would land on the left and the right-aligned Persian title
        // would run under the Close button.
        var (_, right) = TitleBarCaptionLayout.ResolvePadding(207, 0, 1.5, isRightToLeft: true);

        Assert.True(right > Base + 130, "the caption safe area must be on the physical right");
    }

    [Fact]
    public void SameInsets_LandOnOppositeSides_ForTheTwoDirections()
    {
        var ltr = TitleBarCaptionLayout.ResolvePadding(138, 0, 1.0, isRightToLeft: false);
        var rtl = TitleBarCaptionLayout.ResolvePadding(138, 0, 1.0, isRightToLeft: true);

        Assert.Equal(ltr.Left, rtl.Right);
        Assert.Equal(ltr.Right, rtl.Left);
    }

    [Fact]
    public void BothEdgesReserved_IsSupportedInEitherDirection()
    {
        var ltr = TitleBarCaptionLayout.ResolvePadding(138, 40, 1.0, isRightToLeft: false);
        Assert.Equal(Base + 138, ltr.Left);
        Assert.Equal(Base + 40, ltr.Right);

        var rtl = TitleBarCaptionLayout.ResolvePadding(138, 40, 1.0, isRightToLeft: true);
        Assert.Equal(Base + 40, rtl.Left);
        Assert.Equal(Base + 138, rtl.Right);
    }

    [Fact]
    public void NoInset_SymmetricBasePadding_EitherDirection()
    {
        var ltr = TitleBarCaptionLayout.ResolvePadding(0, 0, 1.0, isRightToLeft: false);
        var rtl = TitleBarCaptionLayout.ResolvePadding(0, 0, 1.0, isRightToLeft: true);

        Assert.Equal(Base, ltr.Left);
        Assert.Equal(Base, ltr.Right);
        Assert.Equal(Base, rtl.Left);
        Assert.Equal(Base, rtl.Right);
    }

    [Fact]
    public void MaximizedState_LargerInset_IsApplied()
    {
        var (left, _) = TitleBarCaptionLayout.ResolvePadding(160, 0, 1.0, isRightToLeft: false);

        Assert.Equal(Base + 160, left);
    }

    [Theory]
    [InlineData(96, 1.0)]
    [InlineData(120, 1.25)]
    [InlineData(144, 1.5)]
    [InlineData(168, 1.75)]
    [InlineData(192, 2.0)]
    public void Insets_AreConvertedFromPixelsToDips(uint dpi, double expectedScale)
    {
        var scale = TitleBarCaptionLayout.ScaleFromDpi(dpi);

        Assert.Equal(expectedScale, scale, 5);

        var (left, _) = TitleBarCaptionLayout.ResolvePadding(
            (int)(100 * expectedScale), 0, scale, isRightToLeft: false);

        Assert.Equal(Base + 100, left, 5);
    }

    [Fact]
    public void ZeroDpi_FallsBackToFullScale()
    {
        Assert.Equal(1.0, TitleBarCaptionLayout.ScaleFromDpi(0));
    }

    [Fact]
    public void NonPositiveScale_IsTreatedAsOne()
    {
        var (left, right) = TitleBarCaptionLayout.ResolvePadding(138, 138, 0, isRightToLeft: false);

        Assert.Equal(Base + 138, left);
        Assert.Equal(Base + 138, right);
    }

    [Fact]
    public void NegativeInset_AreIgnored()
    {
        var (left, right) = TitleBarCaptionLayout.ResolvePadding(-10, -20, 1.0, isRightToLeft: false);

        Assert.Equal(Base, left);
        Assert.Equal(Base, right);
    }

    [Fact]
    public void HasCaptionArea_DetectsEitherEdge()
    {
        Assert.True(TitleBarCaptionLayout.HasCaptionArea(138, 0));
        Assert.True(TitleBarCaptionLayout.HasCaptionArea(0, 138));
        Assert.False(TitleBarCaptionLayout.HasCaptionArea(0, 0));
    }
}
