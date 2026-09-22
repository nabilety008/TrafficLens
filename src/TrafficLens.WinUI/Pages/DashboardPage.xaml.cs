using Microsoft.UI.Xaml.Controls;

namespace TrafficLens.WinUI.Pages;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        Placeholder.Configure("DashboardLabel", "WUI-002 will migrate the live dashboard.");
    }
}
