using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
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
    private IReadOnlyList<HistoryChartPoint> _series = Array.Empty<HistoryChartPoint>();
    private long _scaleMax = 1;
    private bool _isUnavailable;
    private string _errorDetail = string.Empty;
    private string _historyErrorDetailText = string.Empty;
    private bool _hasData;

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

    private string _summaryDownloadText = "0 B";
    private string _summaryUploadText = "0 B";
    private string _summaryTotalText = "0 B";

    private bool _isTodaySelected = true;
    private bool _isYesterdaySelected;
    private bool _isLast7DaysSelected;
    private bool _isLast30DaysSelected;
    private bool _isLifetimeSelected;
    private bool _isThisMonthSelected;

    private bool _hasComparison;
    private string _comparisonTodayText = "0 B";
    private string _comparisonYesterdayText = "0 B";
    private string _comparisonDifferenceText = string.Empty;
    private string _comparisonPercentageText = string.Empty;

    public HistoryViewModel(ITrafficHistoryService history, ILocalizationService localization)
    {
        _history = history;
        _localization = localization;
        _dispatcher = Application.Current?.Dispatcher;

        _history.HistoryChanged += OnHistoryChanged;
        _localization.CultureChanged += OnCultureChanged;

        SelectRangeCommand = new RelayCommand(ExecuteSelectRange);
        ExportCsvCommand = new RelayCommand(ExecuteExportCsv);

        RefreshLocalizedStrings();
        ApplySnapshot(_history.GetSnapshot());
    }

    public ICommand SelectRangeCommand { get; }
    public ICommand ExportCsvCommand { get; }

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

    public string HourlyTrafficLabel
    {
        get => _hourlyTrafficLabel;
        private set => SetProperty(ref _hourlyTrafficLabel, value);
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

    public bool IsThisMonthSelected
    {
        get => _isThisMonthSelected;
        private set => SetProperty(ref _isThisMonthSelected, value);
    }

    public bool HasComparison
    {
        get => _hasComparison;
        private set => SetProperty(ref _hasComparison, value);
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
        HourlyTrafficLabel = _localization["HistoryHourlyTrafficLabel"];
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
        IsThisMonthSelected = _selectedRange == HistoryRange.ThisMonth;
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

        var series = _selectedRange switch
        {
            HistoryRange.Today when snapshot.TodayHourly.Count > 0 => MapHourly(snapshot.TodayHourly, culture),
            HistoryRange.ThisMonth => MapDaily(SliceSeries(snapshot.DailySeries, _selectedRange), culture),
            _ => MapDaily(SliceSeries(snapshot.DailySeries, _selectedRange), culture)
        };
        Series = series;
        ChartTitleLabel = _selectedRange == HistoryRange.Today ? HourlyTrafficLabel : DailyTrafficLabel;

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
        if (_selectedRange != HistoryRange.Today || snapshot.DayFraction <= 0 || snapshot.Yesterday.TotalBytes == 0)
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
                p.UploadBytes))
            .ToArray();

    private static IReadOnlyList<HistoryChartPoint> MapHourly(
        IReadOnlyList<HourlyUsagePoint> points,
        CultureInfo culture) =>
        points
            .Select(p => new HistoryChartPoint(
                p.LocalHour.ToString("D2", culture) + ":00",
                p.DownloadBytes,
                p.UploadBytes))
            .ToArray();

    private void ExecuteExportCsv()
    {
        var snapshot = _history.GetSnapshot();
        var series = _selectedRange switch
        {
            HistoryRange.Today when snapshot.TodayHourly.Count > 0 => Array.Empty<DailyUsagePoint>(),
            _ => SliceSeries(snapshot.DailySeries, _selectedRange)
        };

        if (_selectedRange == HistoryRange.Today && snapshot.TodayHourly.Count > 0)
        {
            var hourlyPoints = snapshot.TodayHourly;
            var lines = new List<string> { "Period,DownloadBytes,UploadBytes,TotalBytes" };
            foreach (var p in hourlyPoints)
            {
                lines.Add($"{p.LocalHour:D2}:00,{p.DownloadBytes},{p.UploadBytes},{p.TotalBytes}");
            }
            WriteCsvFile(lines);
        }
        else
        {
            var lines = new List<string> { "Period,DownloadBytes,UploadBytes,TotalBytes" };
            foreach (var p in series)
            {
                lines.Add($"{p.Date:yyyy-MM-dd},{p.DownloadBytes},{p.UploadBytes},{p.TotalBytes}");
            }
            WriteCsvFile(lines);
        }
    }

    private void WriteCsvFile(List<string> lines)
    {
        var defaultName = $"TrafficLens-History-{DateTime.Now:yyyy-MM-dd}.csv";
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = defaultName
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var csv = string.Join("\n", lines);
            File.WriteAllText(dialog.FileName, csv, new UTF8Encoding(true));
        }
        catch (Exception)
        {
            var msgBoxResult = MessageBox.Show(
                ExportFailedLabel,
                ExportCsvLabel,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
