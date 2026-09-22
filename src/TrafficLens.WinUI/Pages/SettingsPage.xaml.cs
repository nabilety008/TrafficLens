using Microsoft.UI.Xaml.Controls;

namespace TrafficLens.WinUI.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Placeholder.Configure("SettingsNavLabel", "WUI-006 will migrate settings.");
    }
}
