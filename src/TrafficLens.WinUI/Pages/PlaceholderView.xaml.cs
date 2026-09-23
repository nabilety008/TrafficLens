using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrafficLens.Core.Localization;

namespace TrafficLens.WinUI.Pages;

public sealed partial class PlaceholderView : UserControl
{
    private readonly ILocalizationService _localization;
    private string _titleKey = string.Empty;
    private string _milestoneHint = string.Empty;

    public PlaceholderView()
    {
        InitializeComponent();
        _localization = App.Services.Provider.GetRequiredService<ILocalizationService>();
        _localization.CultureChanged += OnCultureChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Configure(string titleKey, string milestoneHint)
    {
        _titleKey = titleKey;
        _milestoneHint = milestoneHint;
        ApplyLocalization();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyLocalization();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _localization.CultureChanged -= OnCultureChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(ApplyLocalization);
    }

    private void ApplyLocalization()
    {
        if (string.IsNullOrEmpty(_titleKey))
        {
            return;
        }

        PageTitleText.Text = _localization[_titleKey];
        PageHintText.Text = _localization["PlaceholderDashboardText"];
        MilestoneHintText.Text = _milestoneHint;
    }
}
