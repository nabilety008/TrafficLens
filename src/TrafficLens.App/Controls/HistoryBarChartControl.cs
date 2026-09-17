using System.Globalization;
using System.Windows;
using System.Windows.Media;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.History;

namespace TrafficLens.App.Controls;

/// <summary>
/// Lightweight native WPF daily-traffic bar chart. Renders one download/upload
/// bar pair per day straight into a <see cref="DrawingContext"/> — no chart
/// library and no per-point UI elements. Bars always draw oldest-left →
/// newest-right regardless of the surrounding UI's FlowDirection.
/// </summary>
public sealed class HistoryBarChartControl : FrameworkElement
{
    private const double PlotLeftPad = 64;
    private const double PlotRightPad = 12;
    private const double PlotTopPad = 10;
    private const double PlotBottomPad = 22;

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(IReadOnlyList<DailyUsagePoint>),
        typeof(HistoryBarChartControl),
        new FrameworkPropertyMetadata(Array.Empty<DailyUsagePoint>(), OnLayoutPropertyChanged));

    public static readonly DependencyProperty YAxisMaxProperty = DependencyProperty.Register(
        nameof(YAxisMax),
        typeof(double),
        typeof(HistoryBarChartControl),
        new FrameworkPropertyMetadata(1.0, OnLayoutPropertyChanged));

    public IReadOnlyList<DailyUsagePoint> Points
    {
        get => (IReadOnlyList<DailyUsagePoint>)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public double YAxisMax
    {
        get => (double)GetValue(YAxisMaxProperty);
        set => SetValue(YAxisMaxProperty, value);
    }

    public HistoryBarChartControl()
    {
        FlowDirection = FlowDirection.LeftToRight;
        SizeChanged += (_, _) => InvalidateVisual();
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((HistoryBarChartControl)d).InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var points = Points;
        var max = Math.Max(YAxisMax, 1);

        var plot = new Rect(
            PlotLeftPad,
            PlotTopPad,
            Math.Max(0, ActualWidth - PlotLeftPad - PlotRightPad),
            Math.Max(0, ActualHeight - PlotTopPad - PlotBottomPad));

        var maxBrush = Brush("TextMutedBrush", Color.FromRgb(0x9A, 0x9A, 0xB0));
        var gridBrush = Brush("BorderBrush", Color.FromRgb(0x3F, 0x3F, 0x55));
        var downloadBrush = Brush("AccentBrush", Color.FromRgb(0x4F, 0xC3, 0xF7));
        var uploadBrush = Brush("AccentAltBrush", Color.FromRgb(0x26, 0xA6, 0x9A));

        if (plot.Width <= 0 || plot.Height <= 0 || points.Count == 0)
        {
            return;
        }

        for (var i = 0; i <= 4; i++)
        {
            var fraction = i / 4.0;
            var y = plot.Top + plot.Height * (1 - fraction);
            dc.DrawLine(new Pen(gridBrush, 0.8), new Point(plot.Left, y), new Point(plot.Right, y));
        }

        DrawText(dc, DataSizeFormatter.Format((long)max), plot.Left - 4, plot.Top - 4, maxBrush, rightAligned: true);
        DrawText(dc, "0", plot.Left - 4, plot.Bottom - 12, maxBrush, rightAligned: true);

        var slot = plot.Width / points.Count;
        var barWidth = Math.Max(1.5, Math.Min(slot * 0.3, 22));
        var gap = Math.Min(2, barWidth * 0.2);

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            var center = plot.Left + slot * (i + 0.5);

            DrawBar(dc, plot, center - gap / 2 - barWidth, barWidth, point.DownloadBytes, max, downloadBrush);
            DrawBar(dc, plot, center + gap / 2, barWidth, point.UploadBytes, max, uploadBrush);

            if (ShouldShowLabel(i, points.Count))
            {
                var label = point.Date.ToString("MM-dd", CultureInfo.CurrentCulture);
                DrawText(dc, label, center, plot.Bottom + 4, maxBrush, centerAligned: true);
            }
        }
    }

    private static bool ShouldShowLabel(int index, int count)
    {
        if (count <= 10)
        {
            return true;
        }

        var step = (int)Math.Ceiling(count / 8.0);
        return index % step == 0 || index == count - 1;
    }

    private static void DrawBar(
        DrawingContext dc,
        Rect plot,
        double x,
        double width,
        long value,
        double max,
        Brush brush)
    {
        if (value <= 0 || x + width < plot.Left || x > plot.Right)
        {
            return;
        }

        var height = Math.Min(value, max) / max * plot.Height;
        if (height <= 0)
        {
            return;
        }

        var left = Math.Max(x, plot.Left);
        var right = Math.Min(x + width, plot.Right);
        dc.DrawRectangle(brush, null, new Rect(left, plot.Bottom - height, Math.Max(0, right - left), height));
    }

    private void DrawText(
        DrawingContext dc,
        string text,
        double x,
        double y,
        Brush brush,
        bool rightAligned = false,
        bool centerAligned = false)
    {
        var typeface = new Typeface("Segoe UI");
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            10,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var origin = rightAligned
            ? new Point(x - formatted.Width, y)
            : centerAligned
                ? new Point(x - formatted.Width / 2, y)
                : new Point(x, y);

        dc.DrawText(formatted, origin);
    }

    private static Brush Brush(string resourceKey, Color fallback) =>
        Application.Current?.TryFindResource(resourceKey) as Brush ?? new SolidColorBrush(fallback);
}
