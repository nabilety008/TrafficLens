using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.Models;
using TrafficLens.Network.Process;

namespace TrafficLens.WinUI.ViewModels;

public sealed class ProcessRowViewModel : INotifyPropertyChanged
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

    public ProcessRowViewModel(ProcessInstanceId identity)
    {
        Identity = identity;
    }

    public ProcessInstanceId Identity { get; }

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

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
