using TrafficLens.WinUI.Infrastructure;

namespace TrafficLens.WinUI.Tests;

/// <summary>
/// The title bar keeps clear of the caption buttons because it follows the caption
/// geometry the shell reports, not because of a margin chosen for a given string.
/// </summary>
public class TitleBarCaptionLayoutTests
{
    [Fact]
    public void Layout_DoesNotDependOnTheTitleText()
    {
        // The helper takes only caption geometry and scale. There is no string, and
        // therefore no language, parameter a per-language offset could hide in.
        var parameters = typeof(TitleBarCaptionLayout)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name == nameof(TitleBarCaptionLayout.ResolvePadding))
            .SelectMany(m => m.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        Assert.Equal(new[] { typeof(int), typeof(int), typeof(double) }, parameters);
        Assert.DoesNotContain(typeof(string), parameters);
    }

    [Fact]
    public void English_Ltr_ReservesTheRightEdge()
    {
        var (left, right) = TitleBarCaptionLayout.ResolvePadding(0, 138, 1.0);

        Assert.Equal(TitleBarCaptionLayout.BasePaddingDip, left);
        Assert.Equal(TitleBarCaptionLayout.BasePaddingDip + 138, right);
    }

    [Fact]
    public void Persian_Rtl_ReservesTheLeftEdge()
    {
        // In a right-to-left window the shell puts the caption buttons on the left,
        // so that is the edge the title has to keep clear.
        var (left, right) = TitleBarCaptionLayout.ResolvePadding(138, 0, 1.0);

        Assert.Equal(TitleBarCaptionLayout.BasePaddingDip + 138, left);
        Assert.Equal(TitleBarCaptionLayout.BasePaddingDip, right);
    }

    [Fact]
    public void BothEdgesReserved_IsSupported()
    {
        var (left, right) = TitleBarCaptionLayout.ResolvePadding(138, 138, 1.0);

        Assert.Equal(TitleBarCaptionLayout.BasePaddingDip + 138, left);
        Assert.Equal(TitleBarCaptionLayout.BasePaddingDip + 138, right);
    }

    [Fact]
    public void MaximizedState_LargerInset_IsApplied()
    {
        var (_, right) = TitleBarCaptionLayout.ResolvePadding(0, 160, 1.0);

        Assert.Equal(TitleBarCaptionLayout.BasePaddingDip + 160, right);
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

        var (_, right) = TitleBarCaptionLayout.ResolvePadding(0, (int)(100 * expectedScale), scale);
        Assert.Equal(TitleBarCaptionLayout.BasePaddingDip + 100, right, 5);
    }

    [Fact]
    public void ZeroDpi_FallsBackToFullScale()
    {
        Assert.Equal(1.0, TitleBarCaptionLayout.ScaleFromDpi(0));
    }

    [Fact]
    public void NegativeOrZeroScale_IsTreatedAsOne()
    {
        var (left, right) = TitleBarCaptionLayout.ResolvePadding(138, 138, 0);

        Assert.Equal(TitleBarCaptionLayout.BasePaddingDip + 138, left);
        Assert.Equal(TitleBarCaptionLayout.BasePaddingDip + 138, right);
    }

    [Fact]
    public void NegativeInset_AreIgnored()
    {
        var (left, right) = TitleBarCaptionLayout.ResolvePadding(-10, -20, 1.0);

        Assert.Equal(TitleBarCaptionLayout.BasePaddingDip, left);
        Assert.Equal(TitleBarCaptionLayout.BasePaddingDip, right);
    }

    [Fact]
    public void HasCaptionArea_DetectsEitherEdge()
    {
        Assert.True(TitleBarCaptionLayout.HasCaptionArea(138, 0));
        Assert.True(TitleBarCaptionLayout.HasCaptionArea(0, 138));
        Assert.False(TitleBarCaptionLayout.HasCaptionArea(0, 0));
    }
}
