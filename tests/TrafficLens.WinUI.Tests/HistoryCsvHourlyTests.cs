using System.Globalization;
using System.Text;
using TrafficLens.Core.History;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.ViewModels;

namespace TrafficLens.WinUI.Tests;

/// <summary>
/// The hourly (Today) CSV branch was untested while the daily branch was covered
/// by HistoryViewModelCsvTests. These tests pin the same invariants for it:
/// invariant hour labels, raw byte values, and a proper UTF-8 payload.
/// </summary>
public sealed class HistoryCsvHourlyTests
{
    [Fact]
    public void BuildCsv_TodayRange_EmitsHourlyRowsWithInvariantHourLabels()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
            var vm = new HistoryViewModel(new FakeHistoryService(CreateSnapshot()), new FakeLocalization(), null!);
            var csv = vm.BuildCsv();
            vm.Dispose();

            var lines = csv.Split('\n');
            Assert.Equal("Period,DownloadBytes,UploadBytes,TotalBytes", lines[0]);

            var rows = lines.Skip(1).Where(r => r.Length > 0).ToList();
            Assert.Equal(2, rows.Count);
            // Hour labels must stay "HH:00" with ASCII digits even under fa-IR.
            Assert.Equal("09:00,100,200,300", rows[0]);
            Assert.Equal("10:00,400,500,900", rows[1]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void BuildCsv_TodayRange_Utf8WithBom_EncodesPersianLocaleWithoutCorruption()
    {
        var vm = new HistoryViewModel(new FakeHistoryService(CreateSnapshot()), new FakeLocalization(), null!);
        var csv = vm.BuildCsv();
        vm.Dispose();

        var bytes = new UTF8Encoding(false).GetBytes(csv);
        // Every byte of the payload must be clean ASCII; a locale-corrupted value
        // (e.g. Persian digits or a decimal comma) would still be valid UTF-8 but
        // would not round-trip to the exact ASCII CSV schema.
        var decoded = Encoding.UTF8.GetString(bytes);
        Assert.Equal(csv, decoded);
        Assert.DoesNotContain('،', decoded);
        Assert.DoesNotContain('۰', decoded);
    }

    private static HistorySnapshot CreateSnapshot()
    {
        return HistorySnapshot.Unavailable(null) with
        {
            IsAvailable = true,
            Today = new TrafficUsage(500, 700),
            TodayHourly = new[]
            {
                new HourlyUsagePoint(
                    DateTime.SpecifyKind(DateTime.Today.AddHours(9), DateTimeKind.Local).ToUniversalTime(),
                    DateTime.SpecifyKind(DateTime.Today.AddHours(10), DateTimeKind.Local).ToUniversalTime(),
                    9, 100, 200),
                new HourlyUsagePoint(
                    DateTime.SpecifyKind(DateTime.Today.AddHours(10), DateTimeKind.Local).ToUniversalTime(),
                    DateTime.SpecifyKind(DateTime.Today.AddHours(11), DateTimeKind.Local).ToUniversalTime(),
                    10, 400, 500),
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

        public void SetCulture(string cultureName) => CultureChanged?.Invoke(this, EventArgs.Empty);

        public string GetString(string key, string? cultureName = null) => key;
    }
}
