using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.Infrastructure;
using TrafficLens.WinUI.ViewModels;

namespace TrafficLens.WinUI.Pages;

public sealed partial class ConnectionsPage : Page
{
    private readonly ConnectionsViewModel _viewModel;
    private readonly ILocalizationService _localization;

    public ConnectionsPage()
    {
        InitializeComponent();

        var services = App.Services.Provider;
        _localization = services.GetRequiredService<ILocalizationService>();
        _viewModel = new ConnectionsViewModel(
            services.GetRequiredService<IConnectionProvider>(),
            _localization,
            services.GetRequiredService<ISettingsService>(),
            services.GetRequiredService<Microsoft.UI.Dispatching.DispatcherQueue>(),
            services.GetRequiredService<ProcessIconCache>());

        Bindings.Update();
        ApplyFlowDirection();
        DataContext = _viewModel;

        _localization.CultureChanged += OnCultureChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ConnectionsViewModel ViewModel => _viewModel;

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        _viewModel.SetActive(true);

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.SetActive(false);
        _localization.CultureChanged -= OnCultureChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _viewModel.Dispose();
    }

    private void OnCultureChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyFlowDirection();
            Bindings.Update();
        });

    private void ApplyFlowDirection()
    {
        var direction = _localization.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
        PageScroller.FlowDirection = direction;
        RootLayout.FlowDirection = direction;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        _viewModel.SearchText = SearchBox.Text;

    private void CopyLocalEndpoint_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { DataContext: ConnectionRowViewModel row })
        {
            row.CopyLocalEndpoint();
        }
    }

    private void CopyRemoteEndpoint_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { DataContext: ConnectionRowViewModel row })
        {
            row.CopyRemoteEndpoint();
        }
    }

    private void CopyRemoteIp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { DataContext: ConnectionRowViewModel row })
        {
            row.CopyRemoteIp();
        }
    }

    private void CopyProcessName_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { DataContext: ConnectionRowViewModel row })
        {
            row.CopyProcessName();
        }
    }
}
