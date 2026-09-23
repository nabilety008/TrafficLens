using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.ViewModels;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace TrafficLens.WinUI.Views;

public sealed partial class FloatingWidgetWindow : Window
{
    public const int WidgetWidth = 280;
    public const int WidgetHeight = 110;

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

        AppWindow.Resize(new SizeInt32(WidgetWidth, WidgetHeight));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(RootGrid);

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
        AppWindow.Show();
        Activate();
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

    private void CloseButton_Click(object sender, RoutedEventArgs e) => _viewModel.RequestClose();

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

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
