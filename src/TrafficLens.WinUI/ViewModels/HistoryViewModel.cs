using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using TrafficLens.Core.Conversion;
using TrafficLens.Core.History;
using TrafficLens.Core.Localization;

namespace TrafficLens.WinUI.ViewModels;

public sealed class HistoryViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ITrafficHistoryService _history;
    private readonly ILocalizationService _localization;
    private readonly DispatcherQueue _dispatcherQueue;

    private HistoryRange _selectedRange = HistoryRange.Today;
    private IReadOnlyList<HistoryChartPoint> _series = Array.Empty<HistoryChartPoint>();
    private long _scaleMax = 1;
    private bool _isUnavailable;
    private string _errorDetail = string.Empty;
    private string _historyErrorDetailText = string.Empty;
    private bool _hasData;
    private bool _isActive;
    private bool _disposed;

    private string _historyLabel = string.Empty;
    private string _dailyTrafficLabel = string.Empty;
    private string _hourlyTrafficLabel = string.Empty;
    private string _chartTitleLabel = string.Empty;
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
    private string _thisMonthLabel = string.Empty;
    private string _todayVsYesterdayLabel = string.Empty;
    private string _yesterdayAtThisTimeLabel = string.Empty;
    private string _differenceLabel = string.Empty;
    private string _exportCsvLabel = string.Empty;
    private string _exportSuccessfulLabel = string.Empty;
    private string _exportFailedLabel = string.Empty;
    private string _noComparisonDataLabel = string.Empty;

    private string _todayDownloadText = "0 B";
    private string _todayUploadText = "0 B";
    private string _todayTotalText = "0 B";
    private string _monthDownloadText = "0 B";
    private string _monthUploadText = "0 B";
    private string _monthTotalText = "0 B";

    private bool _hasComparison;
    private string _comparisonTodayText = "0 B";
    private string _comparisonYesterdayText = "0 B";
    private string _comparisonDifferenceText = string.Empty;
    private string _comparisonPercentageText = string.Empty;

    private string _exportStatusText = string.Empty;
    private bool _hasExportStatus;
    private bool _exportFailed;

    public HistoryViewModel(
        ITrafficHistoryService history,
        ILocalizationService localization,
        DispatcherQueue dispatcherQueue)
    {
        _history = history;
        _localization = localization;
        _dispatcherQueue = dispatcherQueue;

        _history.HistoryChanged += OnHistoryChanged;
        _localization.CultureChanged += OnCultureChanged;

        RefreshLocalizedStrings();
        ApplySnapshot(_history.GetSnapshot());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<HistoryChartPoint> Series
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

    public bool IsEmpty => !_hasData || Series.Count == 0;

    public bool HasComparison
    {
        get => _hasComparison;
        private set => SetProperty(ref _hasComparison, value);
    }

    public string HistoryLabel
    {
        get => _historyLabel;
        private set => SetProperty(ref _historyLabel, value);
    }

    public string ChartTitleLabel
    {
        get => _chartTitleLabel;
        private set => SetProperty(ref _chartTitleLabel, value);
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

    public string ThisMonthLabel
    {
        get => _thisMonthLabel;
        private set => SetProperty(ref _thisMonthLabel, value);
    }

    public string TodayVsYesterdayLabel
    {
        get => _todayVsYesterdayLabel;
        private set => SetProperty(ref _todayVsYesterdayLabel, value);
    }

    public string YesterdayAtThisTimeLabel
    {
        get => _yesterdayAtThisTimeLabel;
        private set => SetProperty(ref _yesterdayAtThisTimeLabel, value);
    }

    public string DifferenceLabel
    {
        get => _differenceLabel;
        private set => SetProperty(ref _differenceLabel, value);
    }

    public string ExportCsvLabel
    {
        get => _exportCsvLabel;
        private set => SetProperty(ref _exportCsvLabel, value);
    }

    public string ExportSuccessfulLabel
    {
        get => _exportSuccessfulLabel;
        private set => SetProperty(ref _exportSuccessfulLabel, value);
    }

    public string ExportFailedLabel
    {
        get => _exportFailedLabel;
        private set => SetProperty(ref _exportFailedLabel, value);
    }

    public string NoComparisonDataLabel
    {
        get => _noComparisonDataLabel;
        private set => SetProperty(ref _noComparisonDataLabel, value);
    }

    public string TodayDownloadText
    {
        get => _todayDownloadText;
        private set => SetProperty(ref _todayDownloadText, value);
    }

    public string TodayUploadText
    {
        get => _todayUploadText;
        private set => SetProperty(ref _todayUploadText, value);
    }

    public string TodayTotalText
    {
        get => _todayTotalText;
        private set => SetProperty(ref _todayTotalText, value);
    }

    public string MonthDownloadText
    {
        get => _monthDownloadText;
        private set => SetProperty(ref _monthDownloadText, value);
    }

    public string MonthUploadText
    {
        get => _monthUploadText;
        private set => SetProperty(ref _monthUploadText, value);
    }

    public string MonthTotalText
    {
        get => _monthTotalText;
        private set => SetProperty(ref _monthTotalText, value);
    }

    public string ComparisonTodayText
    {
        get => _comparisonTodayText;
        private set => SetProperty(ref _comparisonTodayText, value);
    }

    public string ComparisonYesterdayText
    {
        get => _comparisonYesterdayText;
        private set => SetProperty(ref _comparisonYesterdayText, value);
    }

    public string ComparisonDifferenceText
    {
        get => _comparisonDifferenceText;
        private set => SetProperty(ref _comparisonDifferenceText, value);
    }

    public string ComparisonPercentageText
    {
        get => _comparisonPercentageText;
        private set => SetProperty(ref _comparisonPercentageText, value);
    }

    public string ExportStatusText
    {
        get => _exportStatusText;
        private set => SetProperty(ref _exportStatusText, value);
    }

    public bool HasExportStatus
    {
        get => _hasExportStatus;
        private set => SetProperty(ref _hasExportStatus, value);
    }

    public bool ExportFailed
    {
        get => _exportFailed;
        private set => SetProperty(ref _exportFailed, value);
    }

    public HistoryRange SelectedRange => _selectedRange;

    public void SetActive(bool active)
    {
        if (_disposed)
        {
            return;
        }

        _isActive = active;
        if (active)
        {
            RunOnUi(() => ApplySnapshot(_history.GetSnapshot()));
        }
    }

    public void SelectRange(HistoryRange range)
    {
        if (range == _selectedRange)
        {
            return;
        }

        _selectedRange = range;
        ApplySnapshot(_history.GetSnapshot());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _isActive = false;
        _history.HistoryChanged -= OnHistoryChanged;
        _localization.CultureChanged -= OnCultureChanged;
    }

    public string BuildCsv()
    {
        var snapshot = _history.GetSnapshot();
        var lines = new List<string> { "Period,DownloadBytes,UploadBytes,TotalBytes" };

        if (_selectedRange == HistoryRange.Today && snapshot.TodayHourly.Count > 0)
        {
            foreach (var p in snapshot.TodayHourly)
            {
                lines.Add($"{p.LocalHour:D2}:00,{p.DownloadBytes},{p.UploadBytes},{p.TotalBytes}");
            }
        }
        else
        {
            foreach (var p in SliceSeries(snapshot.DailySeries, _selectedRange))
            {
                lines.Add($"{p.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)},{p.DownloadBytes},{p.UploadBytes},{p.TotalBytes}");
            }
        }

        return string.Join("\n", lines);
    }

    public void MarkExportSuccess()
    {
        if (_disposed)
        {
            return;
        }

        ExportFailed = false;
        ExportStatusText = ExportSuccessfulLabel;
        HasExportStatus = true;
    }

    public void MarkExportFailure()
    {
        if (_disposed)
        {
            return;
        }

        ExportFailed = true;
        ExportStatusText = ExportFailedLabel;
        HasExportStatus = true;
    }

    public void ClearExportStatus()
    {
        if (_disposed)
        {
            return;
        }

        HasExportStatus = false;
        ExportStatusText = string.Empty;
        ExportFailed = false;
    }

    public string CsvSuggestedFileName =>
        $"TrafficLens-History-{DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv";

    public string CsvFilterLabel => _localization["CsvFileFilterLabel"];

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        if (_disposed || !_isActive)
        {
            return;
        }

        RunOnUi(() => ApplySnapshot(_history.GetSnapshot()));
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        RunOnUi(() =>
        {
            RefreshLocalizedStrings();
            ApplySnapshot(_history.GetSnapshot());
        });
    }

    private void RunOnUi(Action action)
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (!_disposed)
            {
                action();
            }
        });
    }

    private void RefreshLocalizedStrings()
    {
        HistoryLabel = _localization["HistoryLabel"];
        ChartTitleLabel = _selectedRange == HistoryRange.Today
            ? _localization["HistoryHourlyTrafficLabel"]
            : _localization["HistoryDailyTrafficLabel"];
        _dailyTrafficLabel = _localization["HistoryDailyTrafficLabel"];
        _hourlyTrafficLabel = _localization["HistoryHourlyTrafficLabel"];
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
        ThisMonthLabel = _localization["ThisMonthLabel"];
        TodayVsYesterdayLabel = _localization["TodayVsYesterdayLabel"];
        YesterdayAtThisTimeLabel = _localization["YesterdayAtThisTimeLabel"];
        DifferenceLabel = _localization["DifferenceLabel"];
        ExportCsvLabel = _localization["ExportCsvLabel"];
        ExportSuccessfulLabel = _localization["ExportSuccessfulLabel"];
        ExportFailedLabel = _localization["ExportFailedLabel"];
        NoComparisonDataLabel = _localization["NoComparisonDataLabel"];
    }

    private void ApplySnapshot(HistorySnapshot snapshot)
    {
        IsUnavailable = !snapshot.IsAvailable;
        ErrorDetail = IsUnavailable ? _historyErrorDetailText : string.Empty;

        var culture = _localization.CurrentCulture;
        TodayDownloadText = DataSizeFormatter.Format(snapshot.Today.DownloadBytes, culture);
        TodayUploadText = DataSizeFormatter.Format(snapshot.Today.UploadBytes, culture);
        TodayTotalText = DataSizeFormatter.Format(snapshot.Today.TotalBytes, culture);
        MonthDownloadText = DataSizeFormatter.Format(snapshot.ThisMonth.DownloadBytes, culture);
        MonthUploadText = DataSizeFormatter.Format(snapshot.ThisMonth.UploadBytes, culture);
        MonthTotalText = DataSizeFormatter.Format(snapshot.ThisMonth.TotalBytes, culture);

        var series = _selectedRange switch
        {
            HistoryRange.Today when snapshot.TodayHourly.Count > 0 => MapHourly(snapshot.TodayHourly, culture),
            _ => MapDaily(SliceSeries(snapshot.DailySeries, _selectedRange), culture)
        };
        Series = series;
        ChartTitleLabel = _selectedRange == HistoryRange.Today
            ? _hourlyTrafficLabel
            : _dailyTrafficLabel;

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

        UpdateComparison(snapshot);
    }

    private void UpdateComparison(HistorySnapshot snapshot)
    {
        if (snapshot.DayFraction <= 0 || snapshot.Yesterday.TotalBytes == 0)
        {
            HasComparison = false;
            return;
        }

        HasComparison = true;
        var culture = _localization.CurrentCulture;
        var fraction = snapshot.DayFraction;
        var yesterdayEquivalent = new TrafficUsage(
            (long)Math.Round(snapshot.Yesterday.DownloadBytes * fraction),
            (long)Math.Round(snapshot.Yesterday.UploadBytes * fraction));

        ComparisonTodayText = DataSizeFormatter.Format(snapshot.Today.TotalBytes, culture);
        ComparisonYesterdayText = DataSizeFormatter.Format(yesterdayEquivalent.TotalBytes, culture);

        var diff = snapshot.Today.TotalBytes - yesterdayEquivalent.TotalBytes;
        ComparisonDifferenceText = (diff >= 0 ? "+" : "−") + DataSizeFormatter.Format(Math.Abs(diff), culture);

        if (yesterdayEquivalent.TotalBytes > 0)
        {
            var pct = (double)diff / yesterdayEquivalent.TotalBytes * 100;
            ComparisonPercentageText = (pct >= 0 ? "+" : "") + pct.ToString("0.#", culture) + "%";
        }
        else
        {
            ComparisonPercentageText = diff > 0 ? "+" + DataSizeFormatter.Format(diff, culture) : "—";
        }
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
            case HistoryRange.ThisMonth:
                return series.Where(p => p.Date.Month == today.Month && p.Date.Year == today.Year).ToList();
            default:
                return series;
        }
    }

    private static IReadOnlyList<HistoryChartPoint> MapDaily(
        IReadOnlyList<DailyUsagePoint> points,
        CultureInfo culture) =>
        points
            .Select(p => new HistoryChartPoint(
                p.Date.ToString("MM-dd", culture),
                p.DownloadBytes,
                p.UploadBytes,
                p.Date.ToString("yyyy-MM-dd", culture)))
            .ToArray();

    private static IReadOnlyList<HistoryChartPoint> MapHourly(
        IReadOnlyList<HourlyUsagePoint> points,
        CultureInfo culture) =>
        points
            .Select(p => new HistoryChartPoint(
                p.LocalHour.ToString("D2", culture) + ":00",
                p.DownloadBytes,
                p.UploadBytes,
                $"{p.LocalHour.ToString("D2", culture)}:00–{(p.LocalHour + 1) % 24:D2}:00"))
            .ToArray();

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName!);
        return true;
    }
}
