using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.Graph;
using Windows.Foundation;
using Windows.UI;

namespace TrafficLens.WinUI.Controls;

public sealed partial class TrafficGraphView : UserControl
{
    private const double PlotPadding = 8;
    private const double AxisLabelMargin = 52;

    private bool _redrawPending;
    private Point? _lastHoverPosition;
    private Border? _hoverCallout;
    private TextBlock? _hoverTimeText;
    private TextBlock? _hoverDownloadText;
    private TextBlock? _hoverUploadText;

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
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PointerMoved += OnPointerMoved;
        PointerExited += OnPointerExited;
        PointerCanceled += OnPointerExited;
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _redrawPending = false;
        ScheduleRedraw();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _redrawPending = false;
        ClearHover();
    }

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
        if (width <= AxisLabelMargin + PlotPadding + 4 || height <= PlotPadding * 2 + 4)
        {
            return;
        }

        var plotLeft = PlotPadding + AxisLabelMargin;
        var plotTop = PlotPadding;
        var plotWidth = Math.Max(1, width - plotLeft - PlotPadding);
        var plotHeight = Math.Max(1, height - PlotPadding * 2);

        DrawScale(plotLeft, plotTop, plotWidth, plotHeight);

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

        if (_hoverCallout is not null)
        {
            // Re-render the hover state as part of the redraw so the indicator
            // stays aligned with the newest data without a separate timer.
            RedrawHover();
        }
    }

    private void DrawScale(double left, double top, double width, double height)
    {
        var scaleMax = Math.Max(ScaleMax, 1L);
        var culture = LocalizationCulture();
        var labelBrush = GetThemeBrush("TextFillColorSecondaryBrush", Color.FromArgb(0xFF, 0x9A, 0x9A, 0xB0));
        var guideBrush = GetThemeBrush("CardStrokeColorDefaultBrush", Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

        foreach (var level in GraphScaleLabels.Levels(scaleMax))
        {
            var fraction = level.Position switch
            {
                Position.Bottom => 0.0,
                Position.Middle => 0.5,
                _ => 1.0
            };

            var y = top + height - (fraction * height);
            if (level.Position != Position.Bottom)
            {
                PlotCanvas.Children.Add(new Line
                {
                    X1 = left,
                    Y1 = y,
                    X2 = left + width,
                    Y2 = y,
                    Stroke = guideBrush,
                    StrokeThickness = 1
                });
            }

            AddAxisLabel(DataRateAxisFormatter.Format(level.BytesPerSecond, culture), left - 6, y, labelBrush);
        }
    }

    private void AddAxisLabel(string text, double rightEdgeX, double y, Brush brush)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Foreground = brush,
            FlowDirection = FlowDirection.LeftToRight
        };

        PlotCanvas.Children.Add(block);
        block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(block, rightEdgeX - block.DesiredSize.Width);
        Canvas.SetTop(block, y - block.DesiredSize.Height / 2);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= AxisLabelMargin + PlotPadding + 4 || height <= PlotPadding * 2 + 4)
        {
            return;
        }

        var plotLeft = PlotPadding + AxisLabelMargin;
        var plotWidth = Math.Max(1, width - plotLeft - PlotPadding);
        var plotTop = PlotPadding;
        var plotHeight = Math.Max(1, height - PlotPadding * 2);

        var position = e.GetCurrentPoint(this).Position;
        if (position.X < plotLeft || position.X > plotLeft + plotWidth ||
            position.Y < plotTop || position.Y > plotTop + plotHeight)
        {
            ClearHover();
            return;
        }

        var points = Points ?? Array.Empty<TrafficGraphPoint>();
        var windowSeconds = Math.Max(WindowSeconds, 1);
        var reference = ReferenceTime == default ? DateTime.UtcNow : ReferenceTime;

        var sample = GraphHoverResolver.Resolve(
            points,
            position.X - plotLeft,
            plotWidth,
            windowSeconds,
            reference);

        if (sample is null)
        {
            ClearHover();
            return;
        }

        _lastHoverPosition = position;
        ShowHover(sample.Value, plotLeft, plotTop, plotWidth, plotHeight);
    }

    private void ShowHover(GraphHoverSample sample, double plotLeft, double plotTop, double plotWidth, double plotHeight)
    {
        var culture = LocalizationCulture();

        _hoverCallout ??= CreateHoverCallout();
        _hoverTimeText!.Text = sample.Timestamp.ToLocalTime().ToString("HH:mm:ss", culture);
        _hoverDownloadText!.Text = DataRateAxisFormatter.Format(sample.DownloadBytesPerSecond, culture);
        _hoverUploadText!.Text = DataRateAxisFormatter.Format(sample.UploadBytesPerSecond, culture);

        HoverCanvas.Children.Clear();
        HoverCanvas.Children.Add(_hoverCallout);

        var indicatorX = plotLeft + Math.Clamp(HoverIndicatorFraction(sample.Timestamp, plotWidth, Math.Max(WindowSeconds, 1), ReferenceTime == default ? DateTime.UtcNow : ReferenceTime), 0, 1) * plotWidth;

        PlotCanvas.Children.Add(new Line
        {
            X1 = indicatorX,
            Y1 = plotTop,
            X2 = indicatorX,
            Y2 = plotTop + plotHeight,
            Stroke = GetThemeBrush("TextFillColorSecondaryBrush", Color.FromArgb(0xFF, 0x9A, 0x9A, 0xB0)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 2, 2 },
            IsHitTestVisible = false
        });

        // Callout flips sides so it never covers the hovered point or spills
        // out of the plot area.
        var calloutWidth = _hoverCallout.ActualWidth > 0
            ? _hoverCallout.ActualWidth
            : 108;
        var calloutHeight = _hoverCallout.ActualHeight > 0
            ? _hoverCallout.ActualHeight
            : 64;

        var left = indicatorX + 8;
        if (left + calloutWidth > plotLeft + plotWidth)
        {
            left = indicatorX - 8 - calloutWidth;
        }

        var top = plotTop + 4;
        Canvas.SetLeft(_hoverCallout, left);
        Canvas.SetTop(_hoverCallout, top);
    }

    private static double HoverIndicatorFraction(DateTime timestamp, double plotWidth, double windowSeconds, DateTime reference)
    {
        var start = reference - TimeSpan.FromSeconds(windowSeconds);
        var elapsed = (timestamp - start).TotalSeconds;
        if (plotWidth <= 0)
        {
            return 0;
        }

        return Math.Clamp(elapsed / windowSeconds, 0, 1);
    }

    private Border CreateHoverCallout()
    {
        _hoverTimeText = new TextBlock
        {
            FontSize = 11,
            Foreground = GetThemeBrush("TextFillColorPrimaryBrush", Colors.White),
            FlowDirection = FlowDirection.LeftToRight
        };

        _hoverDownloadText = new TextBlock
        {
            FontSize = 11,
            Foreground = GetThemeBrush("AccentFillColorDefaultBrush", Color.FromArgb(0xFF, 0x4F, 0xC3, 0xF7)),
            FlowDirection = FlowDirection.LeftToRight
        };

        _hoverUploadText = new TextBlock
        {
            FontSize = 11,
            Foreground = GetThemeBrush("SuccessFillColorDefaultBrush", Color.FromArgb(0xFF, 0x26, 0xA6, 0x9A)),
            FlowDirection = FlowDirection.LeftToRight
        };

        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(_hoverTimeText);
        panel.Children.Add(_hoverDownloadText);
        panel.Children.Add(_hoverUploadText);

        return new Border
        {
            Padding = new Thickness(8, 6, 8, 6),
            CornerRadius = new CornerRadius(4),
            Background = GetThemeBrush("AcrylicBackgroundFillColorDefaultBrush", Color.FromArgb(0xF2, 0x2B, 0x2B, 0x2B)),
            BorderBrush = GetThemeBrush("CardStrokeColorDefaultBrush", Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            Child = panel,
            IsHitTestVisible = false
        };
    }

    private void RedrawHover()
    {
        // Called after a data redraw while hovering: re-request a hover update
        // against the newest Points without inventing new samples.
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= AxisLabelMargin + PlotPadding + 4 || height <= PlotPadding * 2 + 4)
        {
            return;
        }

        var plotLeft = PlotPadding + AxisLabelMargin;
        var plotWidth = Math.Max(1, width - plotLeft - PlotPadding);
        var plotTop = PlotPadding;
        var plotHeight = Math.Max(1, height - PlotPadding * 2);

        if (_lastHoverPosition is { } position)
        {
            var points = Points ?? Array.Empty<TrafficGraphPoint>();
            var sample = GraphHoverResolver.Resolve(
                points,
                position.X - plotLeft,
                plotWidth,
                Math.Max(WindowSeconds, 1),
                ReferenceTime == default ? DateTime.UtcNow : ReferenceTime);

            if (sample is null)
            {
                ClearHover();
                return;
            }

            ShowHover(sample.Value, plotLeft, plotTop, plotWidth, plotHeight);
        }
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) => ClearHover();

    private void ClearHover()
    {
        _lastHoverPosition = null;
        _hoverCallout = null;
        _hoverTimeText = null;
        _hoverDownloadText = null;
        _hoverUploadText = null;
        HoverCanvas.Children.Clear();
    }

    private static CultureInfo LocalizationCulture() => CultureInfo.CurrentCulture;

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
