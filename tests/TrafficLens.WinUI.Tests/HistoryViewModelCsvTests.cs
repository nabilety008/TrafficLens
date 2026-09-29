using System.Globalization;
using TrafficLens.Core.History;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.ViewModels;

namespace TrafficLens.WinUI.Tests;

public sealed class HistoryViewModelCsvTests
{
    [Fact]
    public void BuildCsv_DailyRows_UseInvariantGregorianDates_UnderPersianCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
            var vm = new HistoryViewModel(new FakeHistoryService(CreateSnapshot()), new FakeLocalization(), null!);
            vm.SelectRange(HistoryRange.Last7Days);
            var csv = vm.BuildCsv();
            vm.Dispose();

            var rows = csv.Split('\n').Skip(1).Where(r => r.Length > 0).ToList();
            Assert.NotEmpty(rows);

            var expectedToday = DateOnly.FromDateTime(DateTime.Today)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var todayRow = rows.Single(r => r.StartsWith(expectedToday + ","));
            Assert.EndsWith(",1000,2000,3000", todayRow);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void CsvSuggestedFileName_UsesInvariantDate_UnderPersianCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
            var vm = new HistoryViewModel(new FakeHistoryService(CreateSnapshot()), new FakeLocalization(), null!);
            var name = vm.CsvSuggestedFileName;
            vm.Dispose();

            var expected = "TrafficLens-History-"
                + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                + ".csv";
            Assert.Equal(expected, name);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Series_PopulatesFullPeriodLabels_ForHover()
    {
        var vm = new HistoryViewModel(new FakeHistoryService(CreateSnapshot()), new FakeLocalization(), null!);

        // Daily (Last 7 Days): full label must be the invariant yyyy-MM-dd date.
        vm.SelectRange(HistoryRange.Last7Days);
        var daily = vm.Series;
        Assert.NotEmpty(daily);
        Assert.All(daily, p =>
        {
            Assert.False(string.IsNullOrEmpty(p.FullPeriodLabel));
            Assert.Equal(10, p.FullPeriodLabel.Length);
            Assert.Contains('-', p.FullPeriodLabel);
        });

        // Hourly (Today): full label must be the hour range like "07:00–08:00".
        vm.SelectRange(HistoryRange.Today);
        var hourly = vm.Series;
        Assert.All(hourly, p => Assert.Matches(@"^\d{2}:00–\d{2}:00$", p.FullPeriodLabel));

        vm.Dispose();
    }

    private static HistorySnapshot CreateSnapshot()
    {
        return HistorySnapshot.Unavailable(null) with
        {
            IsAvailable = true,
            Error = null,
            Today = new TrafficUsage(1000, 2000),
            Last7Days = new TrafficUsage(7000, 8000),
            DailySeries = new[]
            {
                new DailyUsagePoint(DateOnly.FromDateTime(DateTime.Today.AddDays(-3)), 100, 200),
                new DailyUsagePoint(DateOnly.FromDateTime(DateTime.Today.AddDays(-2)), 300, 400),
                new DailyUsagePoint(DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), 500, 600),
                new DailyUsagePoint(DateOnly.FromDateTime(DateTime.Today), 1000, 2000),
            },
            TodayHourly = new[]
            {
                new HourlyUsagePoint(DateTime.Today.AddHours(7), DateTime.Today.AddHours(8), 7, 100, 50),
                new HourlyUsagePoint(DateTime.Today.AddHours(8), DateTime.Today.AddHours(9), 8, 200, 100),
            },
        };
    }

    private sealed class FakeHistoryService : ITrafficHistoryService
    {
        private readonly HistorySnapshot _snapshot;

        public FakeHistoryService(HistorySnapshot snapshot) => _snapshot = snapshot;

        public event EventHandler? HistoryChanged
        {
            add { }
            remove { }
        }

        public bool IsAvailable => true;

        public string? LastError => null;

        public HistorySnapshot GetSnapshot() => _snapshot;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class FakeLocalization : ILocalizationService
    {
        public CultureInfo CurrentCulture => CultureInfo.InvariantCulture;

        public event EventHandler? CultureChanged;

        public string this[string key] => key;

        public bool IsRightToLeft => false;

        public void SetCulture(string cultureName)
        {
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }

        public string GetString(string key, string? cultureName = null) => key;
    }
}
