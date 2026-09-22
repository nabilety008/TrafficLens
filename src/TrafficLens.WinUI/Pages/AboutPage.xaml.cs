using Microsoft.UI.Xaml.Controls;

namespace TrafficLens.WinUI.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        Placeholder.Configure("AboutNavLabel", "WUI-006 will migrate About and diagnostics.");
    }
}
