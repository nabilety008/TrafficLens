using System.Windows.Input;
using TrafficLens.App.Commands;
using TrafficLens.Core.Localization;

namespace TrafficLens.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private string _windowTitle = "TrafficLens";
    private string _statusMessage = string.Empty;

    public MainViewModel(ILocalizationService localization, DashboardViewModel dashboard)
    {
        _localization = localization;
        Dashboard = dashboard;
        _localization.CultureChanged += (_, _) => RefreshLocalizedStrings();
        RefreshLocalizedStrings();

        SwitchToEnglishCommand = new RelayCommand(() => _localization.SetCulture("en-US"));
        SwitchToPersianCommand = new RelayCommand(() => _localization.SetCulture("fa-IR"));
    }

    public DashboardViewModel Dashboard { get; }

    public string WindowTitle
    {
        get => _windowTitle;
        private set => SetProperty(ref _windowTitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand SwitchToEnglishCommand { get; }

    public ICommand SwitchToPersianCommand { get; }

    private void RefreshLocalizedStrings()
    {
        WindowTitle = _localization["WindowTitle"];
        StatusMessage = _localization["StatusReady"];
    }
}