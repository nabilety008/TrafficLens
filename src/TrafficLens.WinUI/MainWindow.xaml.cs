using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.Pages;
using Windows.Graphics;

namespace TrafficLens.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;
    private bool _navigating;

    public MainWindow(ILocalizationService localization, ISettingsService settings)
    {
        InitializeComponent();
        _localization = localization;
        _settings = settings;

        var appWindow = AppWindow;
        appWindow.Resize(new SizeInt32(900, 560));

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        _localization.CultureChanged += OnCultureChanged;
        ApplyLocalization();

        NavView.SelectedItem = DashboardNavItem;
        ContentFrame.Navigate(typeof(DashboardPage));
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyLocalization();
            ApplyFlowDirection();
            PersistLanguage();
        });
    }

    private void ApplyLocalization()
    {
        Title = _localization["WindowTitle"];
        TitleText.Text = _localization["WindowTitle"];
        DashboardNavItem.Content = _localization["DashboardLabel"];
        ApplicationsNavItem.Content = _localization["ApplicationsLabel"];
        ConnectionsNavItem.Content = _localization["ConnectionsLabel"];
        HistoryNavItem.Content = _localization["HistoryLabel"];
        AlertsNavItem.Content = _localization["AlertsNavLabel"];
        SettingsNavItem.Content = _localization["SettingsNavLabel"];
        AboutNavItem.Content = _localization["AboutNavLabel"];
        ApplyFlowDirection();
    }

    private void ApplyFlowDirection()
    {
        RootGrid.FlowDirection = _localization.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    private void PersistLanguage()
    {
        if (!string.Equals(_settings.Language, _localization.CurrentCulture.Name, StringComparison.OrdinalIgnoreCase))
        {
            _settings.Language = _localization.CurrentCulture.Name;
            _settings.Save();
        }
    }

    private void EnglishButton_Click(object sender, RoutedEventArgs e)
    {
        _localization.SetCulture("en-US");
    }

    private void PersianButton_Click(object sender, RoutedEventArgs e)
    {
        _localization.SetCulture("fa-IR");
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_navigating)
        {
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        var pageType = tag switch
        {
            "Dashboard" => typeof(DashboardPage),
            "Applications" => typeof(ApplicationsPage),
            "Connections" => typeof(ConnectionsPage),
            "History" => typeof(HistoryPage),
            "Alerts" => typeof(AlertsPage),
            "Settings" => typeof(SettingsPage),
            "About" => typeof(AboutPage),
            _ => null
        };

        if (pageType is null)
        {
            return;
        }

        _navigating = true;
        try
        {
            ContentFrame.Navigate(pageType);
        }
        finally
        {
            _navigating = false;
        }
    }
}
