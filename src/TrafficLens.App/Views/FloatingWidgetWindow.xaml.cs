using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TrafficLens.App.ViewModels;
using TrafficLens.Core.Localization;

namespace TrafficLens.App.Views;

public partial class FloatingWidgetWindow : Window
{
    private readonly ILocalizationService _localization;

    public FloatingWidgetWindow(FloatingWidgetViewModel viewModel, ILocalizationService localization)
    {
        _localization = localization;
        InitializeComponent();
        DataContext = viewModel;

        _localization.CultureChanged += OnCultureChanged;
        UpdateFlowDirection();
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not ButtonBase)
        {
            DragMove();
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        UpdateFlowDirection();
    }

    private void UpdateFlowDirection()
    {
        FlowDirection = _localization.IsRightToLeft
            ? System.Windows.FlowDirection.RightToLeft
            : System.Windows.FlowDirection.LeftToRight;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _localization.CultureChanged -= OnCultureChanged;
        base.OnClosing(e);
    }
}