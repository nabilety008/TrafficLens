using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using TrafficLens.App.ViewModels;
using TrafficLens.Core.Localization;

namespace TrafficLens.App.Views;

public partial class MainWindow : Window
{
    private readonly ILocalizationService _localization;

    public MainWindow(
        MainViewModel viewModel,
        ILocalizationService localization,
        ApplicationsView applicationsView,
        ConnectionsView connectionsView,
        HistoryView historyView)
    {
        _localization = localization;
        InitializeComponent();
        DataContext = viewModel;
        ApplicationsHost.Content = applicationsView;
        ConnectionsHost.Content = connectionsView;
        HistoryHost.Content = historyView;

        _localization.CultureChanged += OnCultureChanged;
        UpdateFlowDirection();

        Closing += (_, _) =>
        {
            _localization.CultureChanged -= OnCultureChanged;
        };
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        UpdateFlowDirection();
    }

    private void UpdateFlowDirection()
    {
        FlowDirection = _localization.IsRightToLeft
            ? System.Windows.FlowDirection.RightToLeft
            : System.Windows.FlowDirection.LeftToRight;
    }
}