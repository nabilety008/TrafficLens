using System.Windows.Controls;
using TrafficLens.App.ViewModels;

namespace TrafficLens.App.Views;

public partial class ApplicationsView : UserControl
{
    public ApplicationsView(ApplicationsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}