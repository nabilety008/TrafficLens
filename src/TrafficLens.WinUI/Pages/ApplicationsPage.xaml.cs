using Microsoft.UI.Xaml.Controls;

namespace TrafficLens.WinUI.Pages;

public sealed partial class ApplicationsPage : Page
{
    public ApplicationsPage()
    {
        InitializeComponent();
        Placeholder.Configure("ApplicationsLabel", "WUI-003 will migrate per-process traffic.");
    }
}
