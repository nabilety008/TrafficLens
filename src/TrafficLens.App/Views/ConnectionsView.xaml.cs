using System.Windows.Controls;
using TrafficLens.App.ViewModels;

namespace TrafficLens.App.Views;

public partial class ConnectionsView : UserControl
{
    public ConnectionsView(ConnectionsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}