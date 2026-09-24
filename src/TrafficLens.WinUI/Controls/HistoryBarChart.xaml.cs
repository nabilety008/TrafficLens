using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TrafficLens.Core.Conversion;
using TrafficLens.WinUI.ViewModels;
using Windows.UI;

namespace TrafficLens.WinUI.Controls;

public sealed partial class HistoryBarChart : UserControl
{
    private const double PlotLeftPad = 64;
    private const double PlotRightPad = 12;
    private const double PlotTopPad = 10;
    private const double PlotBottomPad = 22;

    private bool _redrawPending;

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(IReadOnlyList<HistoryChartPoint>),
        typeof(HistoryBarChart),
        new PropertyMetadata(Array.Empty<HistoryChartPoint>(), OnVisualDataChanged));

    public static readonly DependencyProperty YAxisMaxProperty = DependencyProperty.Register(
        nameof(YAxisMax),
        typeof(long),
        typeof(HistoryBarChart),
        new PropertyMetadata(1L, OnVisualDataChanged));

    public HistoryBarChart()
    {
        InitializeComponent();
        FlowDirection = FlowDirection.LeftToRight;
        SizeChanged += (_, _) => ScheduleRedraw();
        Loaded += (_, _) => ScheduleRedraw();
        Unloaded += (_, _) => _redrawPending = false;
    }

    public IReadOnlyList<HistoryChartPoint> Points
    {
        get => (IReadOnlyList<HistoryChartPoint>)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public long YAxisMax
    {
        get => (long)GetValue(YAxisMaxProperty);
        set => SetValue(YAxisMaxProperty, value);
    }

    private static void OnVisualDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((HistoryBarChart)d).ScheduleRedraw();

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
        var points = Points ?? Array.Empty<HistoryChartPoint>();
        var max = Math.Max(YAxisMax, 1L);

        if (width <= PlotLeftPad + PlotRightPad || height <= PlotTopPad + PlotBottomPad)
        {
            return;
        }

        var plotLeft = PlotLeftPad;
        var plotTop = PlotTopPad;
        var plotWidth = Math.Max(0, width - PlotLeftPad - PlotRightPad);
        var plotHeight = Math.Max(0, height - PlotTopPad - PlotBottomPad);

        if (plotWidth <= 0 || plotHeight <= 0)
        {
            return;
        }

        var gridBrush = GetThemeBrush("CardStrokeColorDefaultBrush", Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
        var mutedBrush = GetThemeBrush("TextFillColorSecondaryBrush", Color.FromArgb(0xFF, 0x9A, 0x9A, 0xB0));
        var downloadBrush = GetThemeBrush("AccentFillColorDefaultBrush", Color.FromArgb(0xFF, 0x4F, 0xC3, 0xF7));
        var uploadBrush = GetThemeBrush("SuccessFillColorDefaultBrush", Color.FromArgb(0xFF, 0x26, 0xA6, 0x9A));

        for (var i = 0; i <= 4; i++)
        {
            var fraction = i / 4.0;
            var y = plotTop + plotHeight * (1 - fraction);
            PlotCanvas.Children.Add(new Line
            {
                X1 = plotLeft,
                Y1 = y,
                X2 = plotLeft + plotWidth,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1
            });
        }

        AddText(DataSizeFormatter.Format(max), plotLeft - 4, plotTop - 4, mutedBrush, rightAligned: true);
        AddText("0", plotLeft - 4, plotTop + plotHeight - 12, mutedBrush, rightAligned: true);

        if (points.Count == 0)
        {
            return;
        }

        var slot = plotWidth / points.Count;
        var barWidth = Math.Max(1.5, Math.Min(slot * 0.3, 22));
        var gap = Math.Min(2, barWidth * 0.2);

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            var center = plotLeft + slot * (i + 0.5);

            DrawBar(center - gap / 2 - barWidth, barWidth, point.DownloadBytes, max, plotTop, plotHeight, downloadBrush);
            DrawBar(center + gap / 2, barWidth, point.UploadBytes, max, plotTop, plotHeight, uploadBrush);

            if (ShouldShowLabel(i, points.Count))
            {
                AddText(point.Label, center, plotTop + plotHeight + 4, mutedBrush, centerAligned: true);
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

    private void DrawBar(
        double x,
        double width,
        long value,
        long max,
        double plotTop,
        double plotHeight,
        Brush brush)
    {
        if (value <= 0)
        {
            return;
        }

        var height = Math.Min(value, max) / (double)max * plotHeight;
        if (height <= 0)
        {
            return;
        }

        var rect = new Rectangle
        {
            Fill = brush,
            Width = Math.Max(1, width),
            Height = height
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, plotTop + plotHeight - height);
        PlotCanvas.Children.Add(rect);
    }

    private void AddText(string text, double x, double y, Brush brush, bool rightAligned = false, bool centerAligned = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Foreground = brush,
            FlowDirection = FlowDirection.LeftToRight
        };
        PlotCanvas.Children.Add(block);
        block.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));

        var width = block.DesiredSize.Width;
        var left = rightAligned ? x - width : centerAligned ? x - width / 2 : x;
        Canvas.SetLeft(block, left);
        Canvas.SetTop(block, y);
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
