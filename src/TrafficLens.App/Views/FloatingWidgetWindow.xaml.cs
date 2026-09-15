using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TrafficLens.App.ViewModels;

namespace TrafficLens.App.Views;

public partial class FloatingWidgetWindow : Window
{
    public FloatingWidgetWindow(FloatingWidgetViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not ButtonBase)
        {
            DragMove();
        }
    }
}