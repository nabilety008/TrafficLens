using System;
using System.Runtime.InteropServices;

namespace TrafficLens.WinUI.Infrastructure;

/// <summary>
/// Reads the caption-control region a window really has, for the case where the
/// shell reports no title-bar insets at all.
/// </summary>
/// <remarks>
/// <para>
/// A window can report <c>AppWindow.TitleBar.LeftInset</c> and <c>RightInset</c> as
/// zero while still drawing Minimize, Maximize and Close. Nothing is left to reserve
/// then, and the title runs under the controls. The region cannot be recovered from
/// the shell insets in that case, and <c>DWMWA_CAPTION_BUTTON_BOUNDS</c> answers
/// with a zero-width rectangle for such a window, so it is taken from the window's own
/// non-client hit testing instead: <c>WM_NCHITTEST</c> returns HTMINBUTTON /
/// HTMAXBUTTON / HTCLOSE inside the controls and HTCAPTION outside them, which is
/// the same region Windows itself gives the controls for clicks and drag.
/// </para>
/// <para>
/// Every input is the live window and a DPI-aware system metric, so the result follows
/// the monitor scale, the window state and the layout direction without a constant of
/// its own, and it reports the physical side the controls are on. It is a single
/// question asked from the caller's existing window-changed handler, not a poller.
/// </para>
/// <para>
/// The search is bounded on both axes. Each edge is probed over the resize-frame
/// thickness only, so the messages spent finding the controls are
/// <c>reach / step + 1</c> per edge, and each boundary is then found by halving the
/// range, which costs <c>log2(window width)</c>. The worst case is therefore
/// <c>rows x (2 x (reach / step + 1) + 2 x log2(width))</c> - a few dozen messages on
/// a normal window, and a wider window costs one extra halving step rather than a
/// longer scan.
/// </para>
/// <para>
/// The zones are published by the compositor a frame after a resize, so a call made
/// from the window-changed notification can still see the previous frame's answer and
/// report no controls. A false result therefore means "not known yet" rather than
/// "absent", and the caller re-asks once the frame has been produced.
/// </para>
/// </remarks>
internal static class NativeCaptionButtons
{
    private const uint WmNcHitTest = 0x0084;
    private const int HtMinButton = 8;
    private const int HtMaxButton = 9;
    private const int HtClose = 20;
    private const int SmCySmIcon = 12;
    private const int SmCxSizeFrame = 32;
    private const int SmCxPaddedBorder = 92;

    /// <summary>
    /// Measures how much of the window each edge has to give up to the caption
    /// controls, in physical pixels.
    /// </summary>
    /// <param name="window">The window handle to measure.</param>
    /// <param name="dpi">The window DPI, so the probe row scales with the monitor.</param>
    /// <param name="leftInsetPixels">Reserved width on the physical left.</param>
    /// <param name="rightInsetPixels">Reserved width on the physical right.</param>
    /// <returns>False when the window has no caption controls to reserve.</returns>
    public static bool TryGetInsets(
        IntPtr window,
        uint dpi,
        out int leftInsetPixels,
        out int rightInsetPixels)
    {
        leftInsetPixels = 0;
        rightInsetPixels = 0;

        if (window == IntPtr.Zero || !GetWindowRect(window, out var bounds))
        {
            return false;
        }

        var width = bounds.Right - bounds.Left;
        if (width <= 0)
        {
            return false;
        }

        // The controls fill the caption band, whose height is the small-icon metric,
        // so probing inside that band finds them at any monitor scale. Two rows are
        // tried so a band sitting slightly higher or lower is still covered.
        var captionHeight = Math.Max(1, GetSystemMetricsForDpi(SmCySmIcon, (int)dpi));
        var step = Math.Max(4, captionHeight / 6);
        var frame = GetSystemMetricsForDpi(SmCxSizeFrame, (int)dpi) + GetSystemMetricsForDpi(SmCxPaddedBorder, (int)dpi);
        var reach = Math.Max(step, 4 * frame);

        foreach (var row in new[] { captionHeight / 4, captionHeight / 2 })
        {
            if (row < 1)
            {
                continue;
            }

            var fromRight = NearestControlPixel(window, bounds.Left, bounds.Top, width, step, reach, row, fromRight: true);
            var fromLeft = NearestControlPixel(window, bounds.Left, bounds.Top, width, step, reach, row, fromRight: false);

            if (fromRight < 0 && fromLeft < 0)
            {
                continue;
            }

            if (fromRight >= 0)
            {
                rightInsetPixels = width - InnerEdgeFromRight(window, bounds.Left, bounds.Top, fromRight, row);
            }

            if (fromLeft >= 0)
            {
                leftInsetPixels = OuterEdgeFromLeft(window, bounds.Left, bounds.Top, fromLeft, row, width) + 1;
            }

            return true;
        }

        return false;
    }

    private static int NearestControlPixel(
        IntPtr window,
        int originX,
        int originY,
        int width,
        int step,
        int reach,
        int row,
        bool fromRight)
    {
        for (var offset = 0; offset <= reach; offset += step)
        {
            var x = fromRight ? width - 1 - offset : offset;
            if (x < 0 || x >= width)
            {
                break;
            }

            if (IsControl(window, originX, originY, x, row))
            {
                return x;
            }
        }

        return -1;
    }

    /// <summary>Finds the innermost control pixel measured from the right edge.</summary>
    private static int InnerEdgeFromRight(IntPtr window, int originX, int originY, int found, int row)
    {
        if (IsControl(window, originX, originY, 0, row))
        {
            return 0;
        }

        var notControl = 0;
        var control = found;

        while (control - notControl > 1)
        {
            var middle = notControl + (control - notControl) / 2;
            if (IsControl(window, originX, originY, middle, row))
            {
                control = middle;
            }
            else
            {
                notControl = middle;
            }
        }

        return control;
    }

    /// <summary>Finds the outermost control pixel measured from the left edge.</summary>
    private static int OuterEdgeFromLeft(
        IntPtr window,
        int originX,
        int originY,
        int found,
        int row,
        int width)
    {
        if (IsControl(window, originX, originY, width - 1, row))
        {
            return width - 1;
        }

        var control = found;
        var notControl = width - 1;

        while (notControl - control > 1)
        {
            var middle = control + (notControl - control) / 2;
            if (IsControl(window, originX, originY, middle, row))
            {
                control = middle;
            }
            else
            {
                notControl = middle;
            }
        }

        return control;
    }

    private static bool IsControl(IntPtr window, int originX, int originY, int x, int row) =>
        HitCode(window, originX, originY, x, row) is HtMinButton or HtMaxButton or HtClose;

    private static int HitCode(IntPtr window, int originX, int originY, int x, int row)
    {
        var screenX = originX + x;
        var screenY = originY + row;
        var lParam = (IntPtr)(((screenY & 0xFFFF) << 16) | (screenX & 0xFFFF));

        return (int)SendMessage(window, WmNcHitTest, IntPtr.Zero, lParam);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int index, int dpi);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
