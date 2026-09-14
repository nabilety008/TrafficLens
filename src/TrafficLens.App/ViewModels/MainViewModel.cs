using System.Windows.Input;
using TrafficLens.App.Commands;
using TrafficLens.Core.Localization;

namespace TrafficLens.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private string _windowTitle = "TrafficLens";
    private string _statusMessage = string.Empty;
    private string _downloadLabel = string.Empty;
    private string _uploadLabel = string.Empty;
    private string _totalTrafficLabel = string.Empty;
    private string _activeAdapterLabel = string.Empty;
    private string _networkStatusLabel = string.Empty;
    private string _placeholderText = string.Empty;

    public MainViewModel(ILocalizationService localization)
    {
        _localization = localization;
        _localization.CultureChanged += (_, _) => RefreshLocalizedStrings();
        RefreshLocalizedStrings();

        SwitchToEnglishCommand = new RelayCommand(() => _localization.SetCulture("en-US"));
        SwitchToPersianCommand = new RelayCommand(() => _localization.SetCulture("fa-IR"));
    }

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

    public string DownloadLabel
    {
        get => _downloadLabel;
        private set => SetProperty(ref _downloadLabel, value);
    }

    public string UploadLabel
    {
        get => _uploadLabel;
        private set => SetProperty(ref _uploadLabel, value);
    }

    public string TotalTrafficLabel
    {
        get => _totalTrafficLabel;
        private set => SetProperty(ref _totalTrafficLabel, value);
    }

    public string ActiveAdapterLabel
    {
        get => _activeAdapterLabel;
        private set => SetProperty(ref _activeAdapterLabel, value);
    }

    public string NetworkStatusLabel
    {
        get => _networkStatusLabel;
        private set => SetProperty(ref _networkStatusLabel, value);
    }

    public string PlaceholderText
    {
        get => _placeholderText;
        private set => SetProperty(ref _placeholderText, value);
    }

    public ICommand SwitchToEnglishCommand { get; }

    public ICommand SwitchToPersianCommand { get; }

    private void RefreshLocalizedStrings()
    {
        WindowTitle = _localization["WindowTitle"];
        StatusMessage = _localization["StatusReady"];
        DownloadLabel = _localization["DownloadLabel"];
        UploadLabel = _localization["UploadLabel"];
        TotalTrafficLabel = _localization["TotalTrafficLabel"];
        ActiveAdapterLabel = _localization["ActiveAdapterLabel"];
        NetworkStatusLabel = _localization["NetworkStatusLabel"];
        PlaceholderText = _localization["PlaceholderDashboardText"];
    }
}