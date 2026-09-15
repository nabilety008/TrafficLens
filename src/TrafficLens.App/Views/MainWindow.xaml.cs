using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using TrafficLens.App.Services;
using TrafficLens.App.ViewModels;
using TrafficLens.Core.Localization;

namespace TrafficLens.App.Views;

public partial class MainWindow : Window
{
    private readonly ILocalizationService _localization;
    private readonly FloatingWidgetService _floatingWidgetService;
    private readonly MainViewModel _viewModel;

    public MainWindow(
        MainViewModel viewModel,
        ILocalizationService localization,
        FloatingWidgetService floatingWidgetService,
        ApplicationsView applicationsView,
        ConnectionsView connectionsView,
        HistoryView historyView)
    {
        _localization = localization;
        _floatingWidgetService = floatingWidgetService;
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        ApplicationsHost.Content = applicationsView;
        ConnectionsHost.Content = connectionsView;
        HistoryHost.Content = historyView;

        _localization.CultureChanged += OnCultureChanged;
        UpdateFlowDirection();

        Closing += (_, _) =>
        {
            _floatingWidgetService.Dispose();
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
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