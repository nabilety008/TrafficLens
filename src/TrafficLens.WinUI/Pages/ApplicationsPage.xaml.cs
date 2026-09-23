using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.ViewModels;

namespace TrafficLens.WinUI.Pages;

public sealed partial class ApplicationsPage : Page
{
    private readonly ApplicationsViewModel _viewModel;
    private readonly ILocalizationService _localization;

    public ApplicationsPage()
    {
        InitializeComponent();

        var services = App.Services.Provider;
        _localization = services.GetRequiredService<ILocalizationService>();
        _viewModel = new ApplicationsViewModel(
            services.GetRequiredService<IProcessTrafficCollector>(),
            _localization,
            services.GetRequiredService<Microsoft.UI.Dispatching.DispatcherQueue>(),
            services.GetRequiredService<Infrastructure.ProcessIconCache>());

        Bindings.Update();
        ApplyFlowDirection();

        _localization.CultureChanged += OnCultureChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ApplicationsViewModel ViewModel => _viewModel;

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
        RootLayout.FlowDirection = _localization.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        _viewModel.SearchText = SearchBox.Text;

    private void StartMonitoring_Click(object sender, RoutedEventArgs e) =>
        _viewModel.StartMonitoring();

    private void RestartAdmin_Click(object sender, RoutedEventArgs e) =>
        _viewModel.RestartAsAdministrator();
}
