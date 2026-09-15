using System.Globalization;
using System.Windows.Media;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.Models;
using TrafficLens.Network.Process;

namespace TrafficLens.App.ViewModels;

/// <summary>
/// One displayed process instance (keyed by <see cref="ProcessInstanceId"/>:
/// PID + start time). Values are re-formatted in place on every snapshot; the
/// <see cref="SetProperty"/> short-circuit keeps property change storms away
/// when nothing actually changed.
/// </summary>
public sealed class ProcessRowViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private string _pidText = string.Empty;
    private string _stateText = string.Empty;
    private string _downloadRateText = "0 B/s";
    private string _uploadRateText = "0 B/s";
    private string _totalRateText = "0 B/s";
    private string _downloadText = "0 B";
    private string _uploadText = "0 B";
    private string _totalText = "0 B";
    private ImageSource? _icon;

    public ProcessRowViewModel(ProcessInstanceId identity)
    {
        Identity = identity;
    }

    public ProcessInstanceId Identity { get; }

    public string? ExecutablePath { get; private set; }

    public bool IconAvailable { get; private set; }

    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    public string PidText
    {
        get => _pidText;
        private set => SetProperty(ref _pidText, value);
    }

    public string StateText
    {
        get => _stateText;
        private set => SetProperty(ref _stateText, value);
    }

    public ImageSource? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
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

    public string TotalRateText
    {
        get => _totalRateText;
        private set => SetProperty(ref _totalRateText, value);
    }

    public string DownloadText
    {
        get => _downloadText;
        private set => SetProperty(ref _downloadText, value);
    }

    public string UploadText
    {
        get => _uploadText;
        private set => SetProperty(ref _uploadText, value);
    }

    public string TotalText
    {
        get => _totalText;
        private set => SetProperty(ref _totalText, value);
    }

    public void Update(ProcessTrafficSample sample, CultureInfo culture, string runningText, string exitedText)
    {
        Name = sample.ProcessName;
        PidText = sample.ProcessId.ToString(culture);
        IconAvailable = sample.IconAvailable;
        ExecutablePath = sample.ExecutablePath;

        StateText = sample.IsRunning switch
        {
            true => runningText,
            false => exitedText,
            null => string.Empty
        };

        DownloadRateText = DataRateFormatter.FormatAdaptive((long)Math.Round(sample.DownloadBytesPerSecond), culture);
        UploadRateText = DataRateFormatter.FormatAdaptive((long)Math.Round(sample.UploadBytesPerSecond), culture);
        TotalRateText = DataRateFormatter.FormatAdaptive((long)Math.Round(sample.TotalBytesPerSecond), culture);

        DownloadText = DataSizeFormatter.Format(sample.DownloadBytes, culture);
        UploadText = DataSizeFormatter.Format(sample.UploadBytes, culture);
        TotalText = DataSizeFormatter.Format(sample.TotalBytes, culture);
    }
}