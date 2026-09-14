using TrafficLens.Core.Conversion;

namespace TrafficLens.App.ViewModels;

public sealed class AdapterListItemViewModel : ViewModelBase
{
    private string _kindText = string.Empty;
    private string _statusText = string.Empty;
    private string _downloadRateText = "0 B/s";
    private string _uploadRateText = "0 B/s";

    public AdapterListItemViewModel(
        string id,
        string name,
        string kindText,
        string statusText)
    {
        Id = id;
        Name = name;
        _kindText = kindText;
        _statusText = statusText;
    }

    public string Id { get; }

    public string Name { get; }

    public override string ToString() => Name;

    public string KindText
    {
        get => _kindText;
        private set => SetProperty(ref _kindText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string DownloadRateText
    {
        get => _downloadRateText;
        private set => SetProperty(ref _downloadRateText, value);
    }

    public string UploadRateText
    {
        get => _uploadRateText;
        private set => SetProperty(ref _uploadRateText, value);
    }

    public void UpdateRates(long downloadBytesPerSecond, long uploadBytesPerSecond)
    {
        DownloadRateText = DataRateFormatter.FormatAdaptive(downloadBytesPerSecond);
        UploadRateText = DataRateFormatter.FormatAdaptive(uploadBytesPerSecond);
    }

    public void UpdateState(string kindText, string statusText)
    {
        KindText = kindText;
        StatusText = statusText;
    }
}