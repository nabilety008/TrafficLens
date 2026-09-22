using Microsoft.UI.Xaml.Controls;

namespace TrafficLens.WinUI.Pages;

public sealed partial class HistoryPage : Page
{
    public HistoryPage()
    {
        InitializeComponent();
        Placeholder.Configure("HistoryLabel", "WUI-005 will migrate history ranges and the graph.");
    }
}
