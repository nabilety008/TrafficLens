using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Graph;
using TrafficLens.Core.History;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.ViewModels;

namespace TrafficLens.WinUI.Pages;

public sealed partial class DashboardPage : Page
{
    private readonly DashboardViewModel _viewModel;
    private readonly ILocalizationService _localization;

    public DashboardPage()
    {
        InitializeComponent();

        var services = App.Services.Provider;
        _localization = services.GetRequiredService<ILocalizationService>();
        _viewModel = new DashboardViewModel(
            services.GetRequiredService<INetworkTrafficCollector>(),
            services.GetRequiredService<INetworkAdapterProvider>(),
            _localization,
            services.GetRequiredService<Microsoft.UI.Dispatching.DispatcherQueue>(),
            services.GetService<ITrafficHistoryService>(),
            services.GetService<IProcessTrafficCollector>());

        Bindings.Update();
        ApplyLocalization();
        ApplyFlowDirection();
        ApplyGraphData();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _localization.CultureChanged += OnCultureChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    public DashboardViewModel ViewModel => _viewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.SetActive(true);
        ApplyGraphData();
        ApplyResponsiveLayout(ActualWidth);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.SetActive(false);
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _localization.CultureChanged -= OnCultureChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnSizeChanged;
        _viewModel.Dispose();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyResponsiveLayout(ActualWidth);

    private void OnCultureChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyLocalization();
            ApplyFlowDirection();
            ApplyGraphData();
        });

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DashboardViewModel.DownloadLabel):
            case nameof(DashboardViewModel.UploadLabel):
            case nameof(DashboardViewModel.TotalLabel):
            case nameof(DashboardViewModel.ActiveAdapterLabel):
            case nameof(DashboardViewModel.StatusLabel):
            case nameof(DashboardViewModel.TodayAtGlanceLabel):
            case nameof(DashboardViewModel.DownloadTodayLabel):
            case nameof(DashboardViewModel.UploadTodayLabel):
            case nameof(DashboardViewModel.TotalTodayLabel):
            case nameof(DashboardViewModel.TopAppNowLabel):
            case nameof(DashboardViewModel.TopAppDownloadLabel):
            case nameof(DashboardViewModel.TopAppUploadLabel):
            case nameof(DashboardViewModel.GraphLiveTrafficLabel):
            case nameof(DashboardViewModel.GraphLast30SecondsLabel):
            case nameof(DashboardViewModel.GraphLast1MinuteLabel):
            case nameof(DashboardViewModel.GraphLast5MinutesLabel):
            case nameof(DashboardViewModel.GraphNowLabel):
            case nameof(DashboardViewModel.GraphDownloadSeriesLabel):
            case nameof(DashboardViewModel.GraphUploadSeriesLabel):
            case nameof(DashboardViewModel.TunnelAggregateHint):
            case nameof(DashboardViewModel.HasTunnelAdapter):
            case nameof(DashboardViewModel.DashboardLabel):
                DispatcherQueue.TryEnqueue(ApplyLocalization);
                break;

            case nameof(DashboardViewModel.DownloadText):
            case nameof(DashboardViewModel.UploadText):
            case nameof(DashboardViewModel.TotalText):
            case nameof(DashboardViewModel.DownloadMbpsText):
            case nameof(DashboardViewModel.UploadMbpsText):
            case nameof(DashboardViewModel.TotalMbpsText):
            case nameof(DashboardViewModel.ActiveAdapterName):
            case nameof(DashboardViewModel.ActiveAdapterKindText):
            case nameof(DashboardViewModel.ActiveAdapterStatusText):
            case nameof(DashboardViewModel.TodayDownloadText):
            case nameof(DashboardViewModel.TodayUploadText):
            case nameof(DashboardViewModel.TodayTotalText):
            case nameof(DashboardViewModel.HasTodayData):
            case nameof(DashboardViewModel.TopAppName):
            case nameof(DashboardViewModel.TopAppRateText):
            case nameof(DashboardViewModel.TopAppDownloadRateText):
            case nameof(DashboardViewModel.TopAppUploadRateText):
            case nameof(DashboardViewModel.TopAppStatusText):
            case nameof(DashboardViewModel.HasTopApp):
            case nameof(DashboardViewModel.IsTopAppPermissionDenied):
            case nameof(DashboardViewModel.IsTopAppUnavailable):
            case nameof(DashboardViewModel.IsTopAppIdle):
                DispatcherQueue.TryEnqueue(ApplyValues);
                break;

            case nameof(DashboardViewModel.GraphPoints):
            case nameof(DashboardViewModel.GraphScaleMax):
            case nameof(DashboardViewModel.GraphReferenceTime):
            case nameof(DashboardViewModel.SelectedGraphRange):
                DispatcherQueue.TryEnqueue(ApplyGraphData);
                break;
        }
    }

    private void ApplyLocalization()
    {
        PageTitleText.Text = _viewModel.DashboardLabel;
        DownloadLabel.Text = _viewModel.DownloadLabel;
        UploadLabel.Text = _viewModel.UploadLabel;
        TotalLabel.Text = _viewModel.TotalLabel;
        TodayHeader.Text = _viewModel.TodayAtGlanceLabel;
        DownloadTodayLabel.Text = _viewModel.DownloadTodayLabel;
        UploadTodayLabel.Text = _viewModel.UploadTodayLabel;
        TotalTodayLabel.Text = _viewModel.TotalTodayLabel;
        TopAppHeader.Text = _viewModel.TopAppNowLabel;
        TopAppDownloadLabel.Text = _viewModel.TopAppDownloadLabel;
        TopAppUploadLabel.Text = _viewModel.TopAppUploadLabel;
        ActiveAdapterHeader.Text = _viewModel.ActiveAdapterLabel;
        StatusLabel.Text = _viewModel.StatusLabel;
        GraphHeader.Text = _viewModel.GraphLiveTrafficLabel;
        Range30sButton.Content = _viewModel.GraphLast30SecondsLabel;
        Range1mButton.Content = _viewModel.GraphLast1MinuteLabel;
        Range5mButton.Content = _viewModel.GraphLast5MinutesLabel;
        DownloadSeriesLabel.Text = _viewModel.GraphDownloadSeriesLabel;
        UploadSeriesLabel.Text = _viewModel.GraphUploadSeriesLabel;
        GraphNowText.Text = _viewModel.GraphNowLabel;
        AdapterKindLabel.Text = _localization["NetworkAdaptersLabel"];

        TunnelHintText.Text = _viewModel.TunnelAggregateHint;
        TunnelHintText.Visibility = _viewModel.HasTunnelAdapter && !string.IsNullOrEmpty(_viewModel.TunnelAggregateHint)
            ? Visibility.Visible
            : Visibility.Collapsed;

        ApplyValues();
    }

    private void ApplyValues()
    {
        DownloadValueText.Text = _viewModel.DownloadText;
        UploadValueText.Text = _viewModel.UploadText;
        TotalValueText.Text = _viewModel.TotalText;
        DownloadMbpsText.Text = _viewModel.DownloadMbpsText;
        UploadMbpsText.Text = _viewModel.UploadMbpsText;
        TotalMbpsText.Text = _viewModel.TotalMbpsText;

        AdapterNameText.Text = _viewModel.ActiveAdapterName;
        AdapterKindText.Text = _viewModel.ActiveAdapterKindText;
        AdapterStatusText.Text = _viewModel.ActiveAdapterStatusText;

        TodayDownloadValue.Text = _viewModel.TodayDownloadText;
        TodayUploadValue.Text = _viewModel.TodayUploadText;
        TodayTotalValue.Text = _viewModel.TodayTotalText;

        if (_viewModel.IsTopAppPermissionDenied || _viewModel.IsTopAppUnavailable)
        {
            TopAppValuesPanel.Visibility = Visibility.Collapsed;
            TopAppStatusText.Visibility = Visibility.Visible;
            TopAppStatusText.Text = _viewModel.TopAppStatusText;
        }
        else if (_viewModel.HasTopApp)
        {
            TopAppValuesPanel.Visibility = Visibility.Visible;
            TopAppStatusText.Visibility = Visibility.Collapsed;
            TopAppNameText.Text = _viewModel.TopAppName;
            TopAppRateText.Text = _viewModel.TopAppRateText;
            TopAppDownloadValue.Text = _viewModel.TopAppDownloadRateText;
            TopAppUploadValue.Text = _viewModel.TopAppUploadRateText;
        }
        else
        {
            TopAppValuesPanel.Visibility = Visibility.Collapsed;
            TopAppStatusText.Visibility = Visibility.Visible;
            TopAppStatusText.Text = string.IsNullOrEmpty(_viewModel.TopAppStatusText)
                ? _viewModel.TopAppNoDataLabel
                : _viewModel.TopAppStatusText;
        }
    }

    private void ApplyGraphData()
    {
        TrafficGraph.Points = _viewModel.GraphPoints;
        TrafficGraph.ScaleMax = _viewModel.GraphScaleMax;
        TrafficGraph.WindowSeconds = _viewModel.SelectedGraphRange.ToDuration().TotalSeconds;
        TrafficGraph.ReferenceTime = _viewModel.GraphReferenceTime;
    }

    private void ApplyFlowDirection()
    {
        RootLayout.FlowDirection = _localization.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    private void ApplyResponsiveLayout(double width)
    {
        if (width <= 0)
        {
            return;
        }

        var narrow = width < 780;
        if (narrow)
        {
            SummaryGrid.ColumnDefinitions[1].Width = new GridLength(0);
            SummaryGrid.ColumnDefinitions[2].Width = new GridLength(0);
            SummaryGrid.ColumnSpacing = 0;
            SummaryGrid.RowSpacing = 12;

            DownloadCard.SetValue(Grid.ColumnProperty, 0);
            DownloadCard.SetValue(Grid.RowProperty, 0);
            UploadCard.SetValue(Grid.ColumnProperty, 0);
            UploadCard.SetValue(Grid.RowProperty, 1);
            TotalCard.SetValue(Grid.ColumnProperty, 0);
            TotalCard.SetValue(Grid.RowProperty, 2);

            InsightGrid.ColumnDefinitions[1].Width = new GridLength(0);
            InsightGrid.ColumnDefinitions[2].Width = new GridLength(0);
            InsightGrid.ColumnSpacing = 0;
            InsightGrid.RowSpacing = 12;

            TodayCard.SetValue(Grid.ColumnProperty, 0);
            TodayCard.SetValue(Grid.RowProperty, 0);
            TopAppCard.SetValue(Grid.ColumnProperty, 0);
            TopAppCard.SetValue(Grid.RowProperty, 1);
            AdapterCard.SetValue(Grid.ColumnProperty, 0);
            AdapterCard.SetValue(Grid.RowProperty, 2);
        }
        else
        {
            SummaryGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            SummaryGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            SummaryGrid.ColumnSpacing = 12;
            SummaryGrid.RowSpacing = 0;

            DownloadCard.SetValue(Grid.ColumnProperty, 0);
            DownloadCard.SetValue(Grid.RowProperty, 0);
            UploadCard.SetValue(Grid.ColumnProperty, 1);
            UploadCard.SetValue(Grid.RowProperty, 0);
            TotalCard.SetValue(Grid.ColumnProperty, 2);
            TotalCard.SetValue(Grid.RowProperty, 0);

            InsightGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            InsightGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            InsightGrid.ColumnSpacing = 12;
            InsightGrid.RowSpacing = 0;

            TodayCard.SetValue(Grid.ColumnProperty, 0);
            TodayCard.SetValue(Grid.RowProperty, 0);
            TopAppCard.SetValue(Grid.ColumnProperty, 1);
            TopAppCard.SetValue(Grid.RowProperty, 0);
            AdapterCard.SetValue(Grid.ColumnProperty, 2);
            AdapterCard.SetValue(Grid.RowProperty, 0);
        }
    }

    private void Range30s_Click(object sender, RoutedEventArgs e) =>
        _viewModel.SelectGraphRange(GraphTimeRange.ThirtySeconds);

    private void Range1m_Click(object sender, RoutedEventArgs e) =>
        _viewModel.SelectGraphRange(GraphTimeRange.OneMinute);

    private void Range5m_Click(object sender, RoutedEventArgs e) =>
        _viewModel.SelectGraphRange(GraphTimeRange.FiveMinutes);
}
