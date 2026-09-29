using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.Graph;
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
    private Border? _hoverCallout;
    private TextBlock? _hoverPeriodText;
    private TextBlock? _hoverDownloadText;
    private TextBlock? _hoverUploadText;
    private TextBlock? _hoverTotalText;
    private Rectangle? _hoverBarHighlight;
    private int _hoverIndex = -1;

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

    public static readonly DependencyProperty TotalLabelProperty = DependencyProperty.Register(
        nameof(TotalLabel),
        typeof(string),
        typeof(HistoryBarChart),
        new PropertyMetadata(string.Empty, OnVisualDataChanged));

    public HistoryBarChart()
    {
        InitializeComponent();
        FlowDirection = FlowDirection.LeftToRight;
        SizeChanged += (_, _) => ScheduleRedraw();
        Loaded += (_, _) => ScheduleRedraw();
        Unloaded += (_, _) =>
        {
            _redrawPending = false;
            ClearHover();
        };
        PointerMoved += OnPointerMoved;
        PointerExited += OnPointerExited;
        PointerCanceled += OnPointerExited;
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

    public string TotalLabel
    {
        get => (string)GetValue(TotalLabelProperty);
        set => SetValue(TotalLabelProperty, value);
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
            ClearHover();
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

        if (_hoverIndex >= 0)
        {
            // Data changed while hovering (e.g. history refresh): re-show the
            // hover for the same index against the new series, clamped.
            var index = Math.Min(_hoverIndex, points.Count - 1);
            ShowHoverForIndex(index, plotLeft, plotTop, plotWidth, plotHeight);
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

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var width = ActualWidth;
        var height = ActualHeight;
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

        var points = Points ?? Array.Empty<HistoryChartPoint>();
        var index = HistoryBarHoverResolver.ResolveIndex(
            points.Count,
            e.GetCurrentPoint(this).Position.X - plotLeft,
            plotWidth);

        if (index is null)
        {
            ClearHover();
            return;
        }

        _hoverIndex = index.Value;
        ShowHoverForIndex(index.Value, plotLeft, plotTop, plotWidth, plotHeight);
    }

    private void ShowHoverForIndex(int index, double plotLeft, double plotTop, double plotWidth, double plotHeight)
    {
        var points = Points ?? Array.Empty<HistoryChartPoint>();
        if (index < 0 || index >= points.Count)
        {
            ClearHover();
            return;
        }

        var culture = CultureInfo.CurrentCulture;
        var point = points[index];

        _hoverCallout ??= CreateHoverCallout();
        _hoverPeriodText!.Text = string.IsNullOrEmpty(point.FullPeriodLabel)
            ? point.Label
            : point.FullPeriodLabel;
        _hoverDownloadText!.Text = DataSizeFormatter.Format(point.DownloadBytes, culture);
        _hoverUploadText!.Text = DataSizeFormatter.Format(point.UploadBytes, culture);
        _hoverTotalText!.Text = string.IsNullOrEmpty(TotalLabel)
            ? DataSizeFormatter.Format(point.TotalBytes, culture)
            : $"{TotalLabel}: {DataSizeFormatter.Format(point.TotalBytes, culture)}";

        HoverCanvas.Children.Clear();

        var slot = plotWidth / points.Count;
        var center = plotLeft + slot * (index + 0.5);

        // Subtle highlight over the hovered slot; drawn below the callout.
        _hoverBarHighlight ??= CreateHoverHighlight();
        _hoverBarHighlight.Width = Math.Max(1, slot);
        _hoverBarHighlight.Height = plotHeight;
        Canvas.SetLeft(_hoverBarHighlight, plotLeft + slot * index);
        Canvas.SetTop(_hoverBarHighlight, plotTop);
        HoverCanvas.Children.Add(_hoverBarHighlight);
        HoverCanvas.Children.Add(_hoverCallout);

        var calloutWidth = _hoverCallout.ActualWidth > 0 ? _hoverCallout.ActualWidth : 132;
        var left = center + 10;
        if (left + calloutWidth > plotLeft + plotWidth)
        {
            left = center - 10 - calloutWidth;
        }
        left = Math.Max(plotLeft, left);

        Canvas.SetLeft(_hoverCallout, left);
        Canvas.SetTop(_hoverCallout, plotTop + 2);
    }

    private Rectangle CreateHoverHighlight()
    {
        return new Rectangle
        {
            Fill = GetThemeBrush("SubtleFillColorSecondaryBrush", Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
            IsHitTestVisible = false
        };
    }

    private Border CreateHoverCallout()
    {
        _hoverPeriodText = new TextBlock
        {
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
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

        _hoverTotalText = new TextBlock
        {
            FontSize = 11,
            Foreground = GetThemeBrush("TextFillColorPrimaryBrush", Colors.White),
            FlowDirection = FlowDirection.LeftToRight
        };

        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(_hoverPeriodText);
        panel.Children.Add(_hoverDownloadText);
        panel.Children.Add(_hoverUploadText);
        panel.Children.Add(_hoverTotalText);

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

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) => ClearHover();

    private void ClearHover()
    {
        _hoverIndex = -1;
        _hoverCallout = null;
        _hoverPeriodText = null;
        _hoverDownloadText = null;
        _hoverUploadText = null;
        _hoverTotalText = null;
        _hoverBarHighlight = null;
        HoverCanvas.Children.Clear();
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
