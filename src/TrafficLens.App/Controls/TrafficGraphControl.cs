using System.Globalization;
using System.Windows;
using System.Windows.Media;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.Graph;

namespace TrafficLens.App.Controls;

/// <summary>
/// Lightweight native WPF traffic graph. Renders two time-series (download /
/// upload) straight into a <see cref="DrawingContext"/> — no chart library, no
/// per-point UI elements. The timeline always draws oldest-left → newest-right
/// regardless of the surrounding UI's FlowDirection.
/// </summary>
public sealed class TrafficGraphControl : FrameworkElement
{
    private const double PlotLeftPad = 64;
    private const double PlotRightPad = 12;
    private const double PlotTopPad = 10;
    private const double PlotBottomPad = 8;

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(IReadOnlyList<TrafficGraphPoint>),
        typeof(TrafficGraphControl),
        new FrameworkPropertyMetadata(Array.Empty<TrafficGraphPoint>(), OnLayoutPropertyChanged));

    public static readonly DependencyProperty YAxisMaxProperty = DependencyProperty.Register(
        nameof(YAxisMax),
        typeof(double),
        typeof(TrafficGraphControl),
        new FrameworkPropertyMetadata(2048.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty WindowSecondsProperty = DependencyProperty.Register(
        nameof(WindowSeconds),
        typeof(double),
        typeof(TrafficGraphControl),
        new FrameworkPropertyMetadata(60.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ReferenceTimeProperty = DependencyProperty.Register(
        nameof(ReferenceTime),
        typeof(DateTime),
        typeof(TrafficGraphControl),
        new FrameworkPropertyMetadata(DateTime.MinValue, OnLayoutPropertyChanged));

    public static readonly DependencyProperty NowLabelProperty = DependencyProperty.Register(
        nameof(NowLabel),
        typeof(string),
        typeof(TrafficGraphControl),
        new FrameworkPropertyMetadata("now", OnLayoutPropertyChanged));

    public IReadOnlyList<TrafficGraphPoint> Points
    {
        get => (IReadOnlyList<TrafficGraphPoint>)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public double YAxisMax
    {
        get => (double)GetValue(YAxisMaxProperty);
        set => SetValue(YAxisMaxProperty, value);
    }

    public double WindowSeconds
    {
        get => (double)GetValue(WindowSecondsProperty);
        set => SetValue(WindowSecondsProperty, value);
    }

    public DateTime ReferenceTime
    {
        get => (DateTime)GetValue(ReferenceTimeProperty);
        set => SetValue(ReferenceTimeProperty, value);
    }

    public string NowLabel
    {
        get => (string)GetValue(NowLabelProperty);
        set => SetValue(NowLabelProperty, value);
    }

    public TrafficGraphControl()
    {
        FlowDirection = FlowDirection.LeftToRight;
        SizeChanged += (_, _) => InvalidateVisual();
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((TrafficGraphControl)d).InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var max = (long)Math.Max(YAxisMax, 1);
        var points = Points;

        var plot = new Rect(
            PlotLeftPad,
            PlotTopPad,
            Math.Max(0, ActualWidth - PlotLeftPad - PlotRightPad),
            Math.Max(0, ActualHeight - PlotTopPad - PlotBottomPad));

        var maxBrush = Brush("TextMutedBrush", Color.FromRgb(0x9A, 0x9A, 0xB0));
        var gridBrush = Brush("BorderBrush", Color.FromRgb(0x3F, 0x3F, 0x55));
        var downloadBrush = Brush("AccentBrush", Color.FromRgb(0x4F, 0xC3, 0xF7));
        var uploadBrush = Brush("AccentAltBrush", Color.FromRgb(0x26, 0xA6, 0x9A));

        if (plot.Width <= 0 || plot.Height <= 0)
        {
            return;
        }

        for (var i = 0; i <= 4; i++)
        {
            var fraction = i / 4.0;
            var y = plot.Top + plot.Height * (1 - fraction);
            dc.DrawLine(new Pen(gridBrush, 0.8), new Point(plot.Left, y), new Point(plot.Right, y));
        }

        DrawAxisLabel(dc, DataRateFormatter.FormatAdaptive(max), plot.Left - 4, plot.Top - 4, maxBrush, rightAligned: true);
        DrawAxisLabel(dc, "0", plot.Left - 4, plot.Bottom - 12, maxBrush, rightAligned: true);

        var reference = ReferenceTime == DateTime.MinValue
            ? (points.Count > 0 ? points[^1].Timestamp : DateTime.UtcNow)
            : ReferenceTime;

        DrawSeries(dc, plot, points, reference, max, downloadBrush, isDownload: true);
        DrawSeries(dc, plot, points, reference, max, uploadBrush, isDownload: false);

        if (points.Count > 0)
        {
            DrawAxisLabel(dc, NowLabel, plot.Right - 2, plot.Bottom + 2, maxBrush, rightAligned: true);
        }
    }

    private void DrawSeries(
        DrawingContext dc,
        Rect plot,
        IReadOnlyList<TrafficGraphPoint> points,
        DateTime reference,
        long max,
        Brush brush,
        bool isDownload)
    {
        if (points.Count < 2)
        {
            return;
        }

        var window = Math.Max(WindowSeconds, 1);
        var start = reference.AddSeconds(-window);
        var geometry = new StreamGeometry();
        var close = points[0];

        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(PointFor(close, plot, start, window, max, isDownload), isFilled: false, isClosed: false);

            for (var i = 1; i < points.Count; i++)
            {
                var value = isDownload ? points[i].DownloadBytesPerSecond : points[i].UploadBytesPerSecond;
                var x = plot.Left + (points[i].Timestamp - start).TotalSeconds / window * plot.Width;
                var y = plot.Bottom - Math.Min(value, max) / (double)max * plot.Height;
                ctx.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: false);
            }
        }

        dc.DrawGeometry(null, new Pen(brush, 2), geometry);
    }

    private static Point PointFor(
        TrafficGraphPoint point,
        Rect plot,
        DateTime start,
        double window,
        long max,
        bool isDownload)
    {
        var value = isDownload ? point.DownloadBytesPerSecond : point.UploadBytesPerSecond;
        var x = plot.Left + (point.Timestamp - start).TotalSeconds / window * plot.Width;
        if (x < plot.Left)
        {
            x = plot.Left;
        }
        else if (x > plot.Right)
        {
            x = plot.Right;
        }

        var y = plot.Bottom - Math.Min(value, max) / (double)max * plot.Height;
        return new Point(x, y);
    }

    private void DrawAxisLabel(
        DrawingContext dc,
        string text,
        double x,
        double y,
        Brush brush,
        bool rightAligned)
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

        dc.DrawText(formatted, rightAligned ? new Point(x - formatted.Width, y) : new Point(x, y));
    }

    private static Brush Brush(string resourceKey, Color fallback) =>
        Application.Current?.TryFindResource(resourceKey) as Brush ?? new SolidColorBrush(fallback);
}