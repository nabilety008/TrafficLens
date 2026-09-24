using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrafficLens.App.Services;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.ViewModels;

namespace TrafficLens.WinUI.Pages;

public sealed partial class AlertsPage : Page
{
    private readonly AlertsViewModel _viewModel;
    private readonly ILocalizationService _localization;

    public AlertsPage()
    {
        InitializeComponent();

        var services = App.Services.Provider;
        _localization = services.GetRequiredService<ILocalizationService>();
        _viewModel = new AlertsViewModel(
            services.GetRequiredService<IAlertService>(),
            services.GetRequiredService<ISettingsService>(),
            _localization,
            services.GetRequiredService<Microsoft.UI.Dispatching.DispatcherQueue>());

        Bindings.Update();
        ApplyFlowDirection();
        DataContext = _viewModel;

        _localization.CultureChanged += OnCultureChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public AlertsViewModel ViewModel => _viewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.SetActive(true);
        Bindings.Update();
        ApplyFlowDirection();
    }

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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Save();
        Bindings.Update();
    }
}
