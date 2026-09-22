using Microsoft.UI.Xaml.Controls;

namespace TrafficLens.WinUI.Pages;

public sealed partial class ConnectionsPage : Page
{
    public ConnectionsPage()
    {
        InitializeComponent();
        Placeholder.Configure("ConnectionsLabel", "WUI-004 will migrate active connections.");
    }
}
