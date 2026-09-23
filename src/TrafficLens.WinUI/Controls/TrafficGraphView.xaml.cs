using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TrafficLens.Core.Graph;
using Windows.Foundation;
using Windows.UI;

namespace TrafficLens.WinUI.Controls;

public sealed partial class TrafficGraphView : UserControl
{
    private const double PlotPadding = 8;

    private bool _redrawPending;

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(IReadOnlyList<TrafficGraphPoint>),
        typeof(TrafficGraphView),
        new PropertyMetadata(Array.Empty<TrafficGraphPoint>(), OnVisualDataChanged));

    public static readonly DependencyProperty ScaleMaxProperty = DependencyProperty.Register(
        nameof(ScaleMax),
        typeof(long),
        typeof(TrafficGraphView),
        new PropertyMetadata(0L, OnVisualDataChanged));

    public static readonly DependencyProperty WindowSecondsProperty = DependencyProperty.Register(
        nameof(WindowSeconds),
        typeof(double),
        typeof(TrafficGraphView),
        new PropertyMetadata(60d, OnVisualDataChanged));

    public static readonly DependencyProperty ReferenceTimeProperty = DependencyProperty.Register(
        nameof(ReferenceTime),
        typeof(DateTime),
        typeof(TrafficGraphView),
        new PropertyMetadata(default(DateTime), OnVisualDataChanged));

    public TrafficGraphView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => ScheduleRedraw();
        Loaded += (_, _) => ScheduleRedraw();
        Unloaded += (_, _) => _redrawPending = false;
    }

    public IReadOnlyList<TrafficGraphPoint> Points
    {
        get => (IReadOnlyList<TrafficGraphPoint>)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public long ScaleMax
    {
        get => (long)GetValue(ScaleMaxProperty);
        set => SetValue(ScaleMaxProperty, value);
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

    private static void OnVisualDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((TrafficGraphView)d).ScheduleRedraw();

    private void ScheduleRedraw()
    {
        if (_redrawPending)
        {
            return;
        }

        _redrawPending = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _redrawPending = false;
            if (IsLoaded)
            {
                Redraw();
            }
        });
    }

    private void Redraw()
    {
        PlotCanvas.Children.Clear();

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= PlotPadding * 2 + 4 || height <= PlotPadding * 2 + 4)
        {
            return;
        }

        var plotLeft = PlotPadding;
        var plotTop = PlotPadding;
        var plotWidth = Math.Max(1, width - PlotPadding * 2);
        var plotHeight = Math.Max(1, height - PlotPadding * 2);

        DrawGrid(plotLeft, plotTop, plotWidth, plotHeight);

        var points = Points ?? Array.Empty<TrafficGraphPoint>();
        var scaleMax = Math.Max(ScaleMax, 1L);
        var windowSeconds = Math.Max(WindowSeconds, 1);
        var reference = ReferenceTime == default ? DateTime.UtcNow : ReferenceTime;
        var start = reference - TimeSpan.FromSeconds(windowSeconds);

        if (points.Count == 0)
        {
            return;
        }

        var downloadPoints = new PointCollection();
        var uploadPoints = new PointCollection();

        foreach (var point in points)
        {
            var elapsed = (point.Timestamp - start).TotalSeconds;
            if (elapsed < 0)
            {
                continue;
            }

            var x = plotLeft + (elapsed / windowSeconds) * plotWidth;
            var downloadY = plotTop + plotHeight - (ClampRatio(point.DownloadBytesPerSecond, scaleMax) * plotHeight);
            var uploadY = plotTop + plotHeight - (ClampRatio(point.UploadBytesPerSecond, scaleMax) * plotHeight);
            downloadPoints.Add(new Point(x, downloadY));
            uploadPoints.Add(new Point(x, uploadY));
        }

        if (downloadPoints.Count == 0)
        {
            return;
        }

        if (downloadPoints.Count == 1)
        {
            downloadPoints.Add(new Point(plotLeft + plotWidth, downloadPoints[0].Y));
            uploadPoints.Add(new Point(plotLeft + plotWidth, uploadPoints[0].Y));
        }

        var downloadLine = new Polyline
        {
            Points = downloadPoints,
            Stroke = GetThemeBrush("AccentFillColorDefaultBrush", Color.FromArgb(0xFF, 0x4F, 0xC3, 0xF7)),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        var uploadLine = new Polyline
        {
            Points = uploadPoints,
            Stroke = GetThemeBrush("SuccessFillColorDefaultBrush", Color.FromArgb(0xFF, 0x26, 0xA6, 0x9A)),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        PlotCanvas.Children.Add(downloadLine);
        PlotCanvas.Children.Add(uploadLine);
    }

    private void DrawGrid(double left, double top, double width, double height)
    {
        var gridBrush = GetThemeBrush("CardStrokeColorDefaultBrush", Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

        for (var i = 1; i < 4; i++)
        {
            var y = top + (height * i / 4.0);
            PlotCanvas.Children.Add(new Line
            {
                X1 = left,
                Y1 = y,
                X2 = left + width,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1
            });
        }
    }

    private static double ClampRatio(long value, long scaleMax)
    {
        if (value <= 0)
        {
            return 0;
        }

        return Math.Min(1.0, value / (double)scaleMax);
    }

    private static Brush GetThemeBrush(string key, Color fallback)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }
}
