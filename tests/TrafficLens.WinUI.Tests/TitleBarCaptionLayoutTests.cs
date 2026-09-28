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

    // ------------------------------------------------- zero-inset fallback path

    [Fact]
    public void PhysicalInsets_ReserveTheSideThatMeasuredTheControls()
    {
        // The measured fallback region on the real Persian window at 150% DPI: the
        // three controls sat on the physical right, 218 px in from that edge, while
        // the shell reported both insets as zero.
        var (left, right) = TitleBarCaptionLayout.ResolvePhysicalInsets(0, 218, dpiScale: 1.5);

        Assert.Equal(Base, left);
        Assert.Equal(Base + 218 / 1.5, right, 5);
    }

    [Fact]
    public void PhysicalInsets_OnTheControlsSide_StayOnThatSideInBothDirections()
    {
        // The fallback measurement is in window coordinates, so it already carries the
        // physical side. It must not be swapped for a right-to-left window, which
        // would put the reserve on the empty edge and leave the title under the
        // controls in exactly the layout that has to work.
        var ltr = TitleBarCaptionLayout.ResolvePhysicalInsets(0, 218, 1.5);
        var rtl = TitleBarCaptionLayout.ResolvePhysicalInsets(0, 218, 1.5);

        Assert.Equal(ltr.Left, rtl.Left);
        Assert.Equal(ltr.Right, rtl.Right);
        Assert.True(rtl.Right > ltr.Left);
    }

    [Theory]
    [InlineData(96, 1.0)]
    [InlineData(120, 1.25)]
    [InlineData(144, 1.5)]
    [InlineData(168, 1.75)]
    [InlineData(192, 2.0)]
    public void PhysicalInsets_AreConvertedFromPixelsToDips(uint dpi, double expectedScale)
    {
        // The caption controls keep the same width in DIP terms whatever the scale, so
        // the pixel count grows with the scale while the reserved padding stays put. A
        // hardcoded DIP value would be too small on a scaled display and would waste
        // space on a primary one. 128 DIP is exact in pixels at every scale here.
        var scale = TitleBarCaptionLayout.ScaleFromDpi(dpi);
        const double controlsDip = 128;
        var (_, right) = TitleBarCaptionLayout.ResolvePhysicalInsets(
            0, (int)(controlsDip * expectedScale), scale);

        Assert.Equal(expectedScale, scale, 5);
        Assert.Equal(Base + controlsDip, right, 5);
    }

    [Fact]
    public void PhysicalInsets_LeaveAGapBetweenTheTitleAndTheControls()
    {
        // The three controls measured 207 of the 218 physical px the fallback
        // reserves, the rest being the resize frame between them and the window edge.
        // The base padding goes on top of that, so the title stops short of the
        // controls instead of ending flush against the first one.
        const double controlsDip = 207 / 1.5;
        var (_, right) = TitleBarCaptionLayout.ResolvePhysicalInsets(0, 218, dpiScale: 1.5);

        Assert.True(right > controlsDip, "the reserve must clear the measured controls");
        Assert.Equal(Base + (218 - 207) / 1.5, right - controlsDip, 5);
    }

    [Fact]
    public void PhysicalInsets_IgnoreNegativeValuesAndANonPositiveScale()
    {
        var (left, right) = TitleBarCaptionLayout.ResolvePhysicalInsets(-30, 138, dpiScale: 0);

        Assert.Equal(Base, left);
        Assert.Equal(Base + 138, right, 5);
    }

    [Fact]
    public void FallbackPathTakesNoTitleAndNoLanguageInput()
    {
        // The fallback must be driven by window geometry alone. A string parameter is
        // where a per-language margin would otherwise appear, so no part of the layout
        // helper may take one.
        var parameters = typeof(TitleBarCaptionLayout)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name is nameof(TitleBarCaptionLayout.ResolvePadding)
                or nameof(TitleBarCaptionLayout.ResolvePhysicalInsets)
                or nameof(TitleBarCaptionLayout.Resolve))
            .SelectMany(m => m.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        Assert.DoesNotContain(typeof(string), parameters);
        Assert.All(parameters, p => Assert.True(
            p == typeof(int) || p == typeof(int?) || p == typeof(double) || p == typeof(bool),
            $"unexpected fallback input: {p.Name}"));
    }

    // ------------------------------------ choosing between insets and measurement

    [Fact]
    public void ShellInsetOfTheResizeFrameAloneIsNotMistakenForACaptionArea()
    {
        // Measured on the real Persian window: the shell reported a single non-zero
        // inset of 11 physical px, which is only the resize frame, while the three
        // controls sat 218 px in. Treating any positive inset as valid reserved nothing
        // useful and left the title 183 px underneath them.
        var (padding, usedMeasured) = TitleBarCaptionLayout.Resolve(
            leadingInsetPixels: 11,
            trailingInsetPixels: 0,
            measuredLeftInsetPixels: 0,
            measuredRightInsetPixels: 218,
            dpiScale: 1.5,
            isRightToLeft: true);

        Assert.True(usedMeasured);
        Assert.Equal(Base, padding.Left, 5);
        Assert.Equal(Base + 218 / 1.5, padding.Right, 5);
    }

    [Fact]
    public void ZeroInsetsFallBackToTheMeasuredControls()
    {
        var (padding, usedMeasured) = TitleBarCaptionLayout.Resolve(0, 0, 0, 218, 1.5, isRightToLeft: true);

        Assert.True(usedMeasured);
        Assert.Equal(Base + 218 / 1.5, padding.Right, 5);
    }

    [Fact]
    public void ShellInsetsThatCoverTheControlsAreStillPreferred()
    {
        // A window that reports a sufficient caption inset keeps the flow-order aware
        // path, so the measurement never overrides a correct report.
        var (padding, usedMeasured) = TitleBarCaptionLayout.Resolve(
            leadingInsetPixels: 300,
            trailingInsetPixels: 0,
            measuredLeftInsetPixels: 0,
            measuredRightInsetPixels: 218,
            dpiScale: 1.5,
            isRightToLeft: true);

        Assert.False(usedMeasured);
        Assert.Equal(Base + 300 / 1.5, padding.Right, 5);
    }

    [Fact]
    public void ShellInsetsStayInChargeWhenTheControlsCannotBeMeasured()
    {
        // The probe can fail, and a failed probe must never take the reserve away from
        // a window that is reporting its own insets correctly.
        var rtl = TitleBarCaptionLayout.Resolve(207, 0, null, null, 1.5, isRightToLeft: true);
        var ltr = TitleBarCaptionLayout.Resolve(207, 0, null, null, 1.5, isRightToLeft: false);

        Assert.False(rtl.UsedMeasuredControls);
        Assert.Equal(Base + 207 / 1.5, rtl.Padding.Right, 5);
        Assert.Equal(Base + 207 / 1.5, ltr.Padding.Left, 5);
    }

    [Fact]
    public void TheReserveNeverEndsUpSmallerThanTheControlsItMustClear()
    {
        // Whatever the shell claims, the applied reserve has to cover the controls that
        // were actually found, in both layout directions.
        foreach (var rtl in new[] { true, false })
        {
            var (padding, _) = TitleBarCaptionLayout.Resolve(11, 0, 0, 218, 1.5, isRightToLeft: rtl);

            Assert.True(padding.Right >= Base + 207 / 1.5, $"controls not covered, rtl={rtl}");
        }
    }
}
