using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TrafficLens.App.Commands;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.History;
using TrafficLens.Core.Localization;

namespace TrafficLens.App.ViewModels;

/// <summary>
/// History page. Reads an immutable <see cref="HistorySnapshot"/> produced by the
/// history service (all SQL runs off the UI thread) and presents range totals and
/// a native daily bar chart. Range selection and formatting are pure and never
/// touch storage. Storage failures surface as a banner and never affect the rest
/// of the app.
/// </summary>
public sealed class HistoryViewModel : ViewModelBase, IDisposable
{
    private readonly ITrafficHistoryService _history;
    private readonly ILocalizationService _localization;
    private readonly Dispatcher? _dispatcher;

    private HistoryRange _selectedRange = HistoryRange.Today;
    private IReadOnlyList<DailyUsagePoint> _series = Array.Empty<DailyUsagePoint>();
    private long _scaleMax = 1;
    private bool _isUnavailable;
    private string _errorDetail = string.Empty;
    private string _historyErrorDetailText = string.Empty;
    private bool _hasData;

    private string _historyLabel = string.Empty;
    private string _dailyTrafficLabel = string.Empty;
    private string _downloadLabel = string.Empty;
    private string _uploadLabel = string.Empty;
    private string _totalLabel = string.Empty;
    private string _noDataText = string.Empty;
    private string _unavailableText = string.Empty;
    private string _todayLabel = string.Empty;
    private string _yesterdayLabel = string.Empty;
    private string _last7DaysLabel = string.Empty;
    private string _last30DaysLabel = string.Empty;
    private string _lifetimeLabel = string.Empty;

    private string _summaryDownloadText = "0 B";
    private string _summaryUploadText = "0 B";
    private string _summaryTotalText = "0 B";

    private bool _isTodaySelected = true;
    private bool _isYesterdaySelected;
    private bool _isLast7DaysSelected;
    private bool _isLast30DaysSelected;
    private bool _isLifetimeSelected;

    public HistoryViewModel(ITrafficHistoryService history, ILocalizationService localization)
    {
        _history = history;
        _localization = localization;
        _dispatcher = Application.Current?.Dispatcher;

        _history.HistoryChanged += OnHistoryChanged;
        _localization.CultureChanged += OnCultureChanged;

        SelectRangeCommand = new RelayCommand(ExecuteSelectRange);

        RefreshLocalizedStrings();
        ApplySnapshot(_history.GetSnapshot());
    }

    public ICommand SelectRangeCommand { get; }

    public void Dispose()
    {
        _history.HistoryChanged -= OnHistoryChanged;
        _localization.CultureChanged -= OnCultureChanged;
    }

    public string HistoryLabel
    {
        get => _historyLabel;
        private set => SetProperty(ref _historyLabel, value);
    }

    public string DailyTrafficLabel
    {
        get => _dailyTrafficLabel;
        private set => SetProperty(ref _dailyTrafficLabel, value);
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

    public string TotalLabel
    {
        get => _totalLabel;
        private set => SetProperty(ref _totalLabel, value);
    }

    public string NoDataText
    {
        get => _noDataText;
        private set => SetProperty(ref _noDataText, value);
    }

    public string UnavailableText
    {
        get => _unavailableText;
        private set => SetProperty(ref _unavailableText, value);
    }

    public string TodayLabel
    {
        get => _todayLabel;
        private set => SetProperty(ref _todayLabel, value);
    }

    public string YesterdayLabel
    {
        get => _yesterdayLabel;
        private set => SetProperty(ref _yesterdayLabel, value);
    }

    public string Last7DaysLabel
    {
        get => _last7DaysLabel;
        private set => SetProperty(ref _last7DaysLabel, value);
    }

    public string Last30DaysLabel
    {
        get => _last30DaysLabel;
        private set => SetProperty(ref _last30DaysLabel, value);
    }

    public string LifetimeLabel
    {
        get => _lifetimeLabel;
        private set => SetProperty(ref _lifetimeLabel, value);
    }

    public string SummaryDownloadText
    {
        get => _summaryDownloadText;
        private set => SetProperty(ref _summaryDownloadText, value);
    }

    public string SummaryUploadText
    {
        get => _summaryUploadText;
        private set => SetProperty(ref _summaryUploadText, value);
    }

    public string SummaryTotalText
    {
        get => _summaryTotalText;
        private set => SetProperty(ref _summaryTotalText, value);
    }

    public IReadOnlyList<DailyUsagePoint> Series
    {
        get => _series;
        private set => SetProperty(ref _series, value);
    }

    public long ScaleMax
    {
        get => _scaleMax;
        private set => SetProperty(ref _scaleMax, value);
    }

    public bool IsUnavailable
    {
        get => _isUnavailable;
        private set => SetProperty(ref _isUnavailable, value);
    }

    public string ErrorDetail
    {
        get => _errorDetail;
        private set => SetProperty(ref _errorDetail, value);
    }

    public bool HasData
    {
        get => _hasData;
        private set
        {
            if (SetProperty(ref _hasData, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public bool IsEmpty => !_hasData;

    public bool IsTodaySelected
    {
        get => _isTodaySelected;
        private set => SetProperty(ref _isTodaySelected, value);
    }

    public bool IsYesterdaySelected
    {
        get => _isYesterdaySelected;
        private set => SetProperty(ref _isYesterdaySelected, value);
    }

    public bool IsLast7DaysSelected
    {
        get => _isLast7DaysSelected;
        private set => SetProperty(ref _isLast7DaysSelected, value);
    }

    public bool IsLast30DaysSelected
    {
        get => _isLast30DaysSelected;
        private set => SetProperty(ref _isLast30DaysSelected, value);
    }

    public bool IsLifetimeSelected
    {
        get => _isLifetimeSelected;
        private set => SetProperty(ref _isLifetimeSelected, value);
    }

    private void OnHistoryChanged(object? sender, EventArgs e) =>
        RunOnUi(() => ApplySnapshot(_history.GetSnapshot()));

    private void OnCultureChanged(object? sender, EventArgs e) =>
        RunOnUi(() =>
        {
            RefreshLocalizedStrings();
            ApplySnapshot(_history.GetSnapshot());
        });

    private void RunOnUi(Action action)
    {
        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.InvokeAsync(action);
    }

    private void RefreshLocalizedStrings()
    {
        HistoryLabel = _localization["HistoryLabel"];
        DailyTrafficLabel = _localization["HistoryDailyTrafficLabel"];
        DownloadLabel = _localization["DownloadLabel"];
        UploadLabel = _localization["UploadLabel"];
        TotalLabel = _localization["TotalRateLabel"];
        NoDataText = _localization["HistoryNoDataLabel"];
        UnavailableText = _localization["HistoryUnavailableLabel"];
        _historyErrorDetailText = _localization["HistoryErrorDetailLabel"];
        TodayLabel = _localization["TodayLabel"];
        YesterdayLabel = _localization["YesterdayLabel"];
        Last7DaysLabel = _localization["Last7DaysLabel"];
        Last30DaysLabel = _localization["Last30DaysLabel"];
        LifetimeLabel = _localization["LifetimeLabel"];
    }

    private void ExecuteSelectRange(object? parameter)
    {
        if (parameter is string text && int.TryParse(text, out var value) && Enum.IsDefined(typeof(HistoryRange), value))
        {
            SelectedRange = (HistoryRange)value;
        }
    }

    private HistoryRange SelectedRange
    {
        get => _selectedRange;
        set
        {
            if (value == _selectedRange)
            {
                return;
            }

            _selectedRange = value;
            UpdateSelectionFlags();
            ApplySnapshot(_history.GetSnapshot());
        }
    }

    private void UpdateSelectionFlags()
    {
        IsTodaySelected = _selectedRange == HistoryRange.Today;
        IsYesterdaySelected = _selectedRange == HistoryRange.Yesterday;
        IsLast7DaysSelected = _selectedRange == HistoryRange.Last7Days;
        IsLast30DaysSelected = _selectedRange == HistoryRange.Last30Days;
        IsLifetimeSelected = _selectedRange == HistoryRange.Lifetime;
    }

    private void ApplySnapshot(HistorySnapshot snapshot)
    {
        IsUnavailable = !snapshot.IsAvailable;
        ErrorDetail = IsUnavailable ? _historyErrorDetailText : string.Empty;

        UpdateSelectionFlags();

        var usage = snapshot.For(_selectedRange);
        var culture = _localization.CurrentCulture;
        SummaryDownloadText = DataSizeFormatter.Format(usage.DownloadBytes, culture);
        SummaryUploadText = DataSizeFormatter.Format(usage.UploadBytes, culture);
        SummaryTotalText = DataSizeFormatter.Format(usage.TotalBytes, culture);

        var series = SliceSeries(snapshot.DailySeries, _selectedRange);
        Series = series;

        long max = 1;
        foreach (var point in series)
        {
            if (point.TotalBytes > max)
            {
                max = point.TotalBytes;
            }
        }

        ScaleMax = max;
        HasData = snapshot.Lifetime.TotalBytes > 0;
    }

    private static IReadOnlyList<DailyUsagePoint> SliceSeries(
        IReadOnlyList<DailyUsagePoint> series,
        HistoryRange range)
    {
        if (series.Count == 0)
        {
            return Array.Empty<DailyUsagePoint>();
        }

        var today = series[^1].Date;

        switch (range)
        {
            case HistoryRange.Today:
                return series.Where(p => p.Date == today).ToList();
            case HistoryRange.Yesterday:
                var yesterday = today.AddDays(-1);
                return series.Where(p => p.Date == yesterday).ToList();
            case HistoryRange.Last7Days:
                return series.Skip(Math.Max(0, series.Count - HistoryRangeCalculator.Last7DayCount)).ToList();
            default:
                return series;
        }
    }
}
