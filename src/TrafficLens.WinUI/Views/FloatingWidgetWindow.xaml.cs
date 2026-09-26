using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.Infrastructure;
using TrafficLens.WinUI.ViewModels;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace TrafficLens.WinUI.Views;

public sealed partial class FloatingWidgetWindow : Window
{
    public const int WidgetWidth = 340;
    public const int WidgetHeight = 140;

    private const double BaseDpi = 96.0;
    private const int GwlStyleIndex = -16;
    private const int WsMaximizeBoxBit = 0x00010000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const int SwRestore = 9;

    private readonly ILocalizationService _localization;
    private readonly FloatingWidgetViewModel _viewModel;
    private bool _dragging;
    private double _dragStartX;
    private double _dragStartY;
    private int _windowStartX;
    private int _windowStartY;
    private bool _forceClose;

    public FloatingWidgetWindow(FloatingWidgetViewModel viewModel, ILocalizationService localization)
    {
        _viewModel = viewModel;
        _localization = localization;
        InitializeComponent();

        ApplyFixedSize();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(RootGrid);
        DisableMaximize();
        WindowIcon.Apply(this);

        RootGrid.DataContext = _viewModel;
        TitleText.Text = _viewModel.WidgetTitleLabel;
        PinButton.Content = _viewModel.PinIcon;
        Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(PinButton, _viewModel.AlwaysOnTopLabel);
        DownloadLabelText.Text = _viewModel.DownloadLabel;
        UploadLabelText.Text = _viewModel.UploadLabel;
        TotalLabelText.Text = _viewModel.TotalLabel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _localization.CultureChanged += OnCultureChanged;
        AppWindow.Closing += OnAppWindowClosing;

        UpdateFlowDirection();
    }

    public void ShowWidget()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        ApplyFixedSize();
        AppWindow.Show();
        if (IsIconic(hwnd))
        {
            ShowWindow(hwnd, SwRestore);
        }

        Activate();
    }

    private void ApplyFixedSize()
    {
        var scale = Math.Max(1.0, GetDpiForWindow(WindowNative.GetWindowHandle(this)) / BaseDpi);
        AppWindow.Resize(new SizeInt32(
            (int)Math.Round(WidgetWidth * scale),
            (int)Math.Round(WidgetHeight * scale)));
    }

    private void DisableMaximize()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var style = GetWindowLongPtr(hwnd, GwlStyleIndex).ToInt64();
        var updated = style & ~WsMaximizeBoxBit;
        if (updated == style)
        {
            return;
        }

        SetWindowLongPtr(hwnd, GwlStyleIndex, (IntPtr)updated);
        SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    public void HideWidget() => AppWindow.Hide();

    public void CloseWidget()
    {
        _forceClose = true;
        Close();
    }

    public void SetAlwaysOnTop(bool alwaysOnTop)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var insertAfter = alwaysOnTop ? new IntPtr(-1) : new IntPtr(-2);
        GetWindowRect(hwnd, out var rect);
        SetWindowPos(hwnd, insertAfter, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, 0x0040);
    }

    public void MoveTo(int x, int y) => AppWindow.Move(new PointInt32(x, y));

    public (int X, int Y) GetPosition()
    {
        var pos = AppWindow.Position;
        return (pos.X, pos.Y);
    }

    private void OnAppWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_forceClose)
        {
            return;
        }

        args.Cancel = true;
        HideWidget();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(FloatingWidgetViewModel.WidgetTitleLabel):
                    TitleText.Text = _viewModel.WidgetTitleLabel;
                    break;
                case nameof(FloatingWidgetViewModel.PinIcon):
                    PinButton.Content = _viewModel.PinIcon;
                    break;
                case nameof(FloatingWidgetViewModel.AlwaysOnTopLabel):
                    Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(PinButton, _viewModel.AlwaysOnTopLabel);
                    break;
                case nameof(FloatingWidgetViewModel.DownloadLabel):
                    DownloadLabelText.Text = _viewModel.DownloadLabel;
                    break;
                case nameof(FloatingWidgetViewModel.DownloadText):
                    DownloadValueText.Text = _viewModel.DownloadText;
                    break;
                case nameof(FloatingWidgetViewModel.UploadLabel):
                    UploadLabelText.Text = _viewModel.UploadLabel;
                    break;
                case nameof(FloatingWidgetViewModel.UploadText):
                    UploadValueText.Text = _viewModel.UploadText;
                    break;
                case nameof(FloatingWidgetViewModel.TotalLabel):
                    TotalLabelText.Text = _viewModel.TotalLabel;
                    break;
                case nameof(FloatingWidgetViewModel.TotalText):
                    TotalValueText.Text = _viewModel.TotalText;
                    break;
            }
        });
    }

    private void OnCultureChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdateFlowDirection);

    private void UpdateFlowDirection()
    {
        RootGrid.FlowDirection = _localization.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    private void PinButton_Click(object sender, RoutedEventArgs e) => _viewModel.TogglePin();

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.OriginalSource is Microsoft.UI.Xaml.Controls.Button)
        {
            return;
        }

        var point = e.GetCurrentPoint(RootGrid);
        if (point.Properties.IsLeftButtonPressed)
        {
            _dragging = true;
            _dragStartX = point.Position.X;
            _dragStartY = point.Position.Y;
            var pos = AppWindow.Position;
            _windowStartX = pos.X;
            _windowStartY = pos.Y;
            RootBorder.CapturePointer(e.Pointer);
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var point = e.GetCurrentPoint(RootGrid);
        var deltaX = (int)(point.Position.X - _dragStartX);
        var deltaY = (int)(point.Position.Y - _dragStartY);
        AppWindow.Move(new PointInt32(_windowStartX + deltaX, _windowStartY + deltaY));
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _dragging = false;
        RootBorder.ReleasePointerCapture(e.Pointer);
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
