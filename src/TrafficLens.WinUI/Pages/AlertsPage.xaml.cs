using Microsoft.UI.Xaml.Controls;

namespace TrafficLens.WinUI.Pages;

public sealed partial class AlertsPage : Page
{
    public AlertsPage()
    {
        InitializeComponent();
        Placeholder.Configure("AlertsNavLabel", "WUI-006 will migrate configured and triggered alerts.");
    }
}
