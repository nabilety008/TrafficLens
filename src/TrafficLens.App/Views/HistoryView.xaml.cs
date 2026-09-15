using System.Windows.Controls;
using TrafficLens.App.ViewModels;

namespace TrafficLens.App.Views;

public partial class HistoryView : UserControl
{
    public HistoryView(HistoryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
