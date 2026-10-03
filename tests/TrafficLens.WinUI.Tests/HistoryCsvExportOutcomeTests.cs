using System.Globalization;
using TrafficLens.Core.History;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.ViewModels;

namespace TrafficLens.WinUI.Tests;

/// <summary>
/// Guards the History CSV export against the false "error saving/exporting"
/// report. The write itself was never the problem: the success-state update ran
/// inside the same try whose catch reported an export failure, and the view
/// model's export-status methods kept raising PropertyChanged after Dispose,
/// which is what a torn-down page sees when the modal picker has closed it.
/// </summary>
public sealed class HistoryCsvExportOutcomeTests
{
    [Fact]
    public void MarkExportSuccess_OnALiveViewModel_ReportsSuccessAndIsVisible()
    {
        var vm = CreateViewModel();

        vm.MarkExportSuccess();

        Assert.False(vm.ExportFailed);
        Assert.True(vm.HasExportStatus);
        Assert.Equal(vm.ExportSuccessfulLabel, vm.ExportStatusText);
    }

    [Fact]
    public void MarkExportFailure_OnALiveViewModel_StillReportsARealFailure()
    {
        // The fix must not suppress genuine write failures.
        var vm = CreateViewModel();

        vm.MarkExportFailure();

        Assert.True(vm.ExportFailed);
        Assert.True(vm.HasExportStatus);
        Assert.Equal(vm.ExportFailedLabel, vm.ExportStatusText);
    }

    [Fact]
    public void CancelledExport_LeavesNoStatusAtAll()
    {
        // Cancellation returns before any status call, so the banner stays hidden
        // and nothing is logged. A fresh view model is exactly that state.
        var vm = CreateViewModel();

        Assert.False(vm.HasExportStatus);
        Assert.False(vm.ExportFailed);
        Assert.Equal(string.Empty, vm.ExportStatusText);
    }

    [Fact]
    public void ClearExportStatus_HidesTheBanner()
    {
        var vm = CreateViewModel();
        vm.MarkExportSuccess();

        vm.ClearExportStatus();

        Assert.False(vm.HasExportStatus);
        Assert.False(vm.ExportFailed);
        Assert.Equal(string.Empty, vm.ExportStatusText);
    }

    [Fact]
    public void SuccessThenFailure_LastOutcomeWins()
    {
        var vm = CreateViewModel();

        vm.MarkExportSuccess();
        vm.MarkExportFailure();

        Assert.True(vm.ExportFailed);
        Assert.Equal(vm.ExportFailedLabel, vm.ExportStatusText);
    }

    [Fact]
    public void ExportStatusMethods_AfterDispose_AreInertAndRaiseNothing()
    {
        // This is the root cause: the page disposes the view model when the modal
        // file picker unloads it, and the in-flight export then pushed status into
        // a dead page, whose failure surfaced as a false export error.
        var vm = CreateViewModel();
        var announced = new List<string>();
        vm.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

        vm.Dispose();
        announced.Clear();

        vm.MarkExportSuccess();
        vm.MarkExportFailure();
        vm.ClearExportStatus();

        Assert.Empty(announced);
    }

    [Fact]
    public void MarkExportSuccess_AfterDispose_DoesNotThrow()
    {
        var vm = CreateViewModel();
        vm.Dispose();

        Assert.Null(Record.Exception(vm.MarkExportSuccess));
        Assert.Null(Record.Exception(vm.MarkExportFailure));
        Assert.Null(Record.Exception(vm.ClearExportStatus));
    }

    [Fact]
    public void BuildCsv_AfterDispose_StillDoesNotThrow()
    {
        var vm = CreateViewModel();
        vm.Dispose();

        Assert.Null(Record.Exception(vm.BuildCsv));
    }

    [Fact]
    public void BuildCsv_ContractIsHeaderPlusRawIntegerByteCounts()
    {
        var vm = CreateViewModel();
        vm.SelectRange(HistoryRange.Last7Days);

        var lines = vm.BuildCsv().Split('\n');

        Assert.Equal("Period,DownloadBytes,UploadBytes,TotalBytes", lines[0]);

        var row = lines.Skip(1).First(l => l.Length > 0);
        var parts = row.Split(',');
        Assert.Equal(4, parts.Length);
        Assert.True(long.TryParse(parts[1], out _), "DownloadBytes must be a raw integer");
        Assert.True(long.TryParse(parts[2], out _), "UploadBytes must be a raw integer");
        Assert.True(long.TryParse(parts[3], out _), "TotalBytes must be a raw integer");

        // Never a formatted data size: no unit suffix, no separators.
        Assert.DoesNotContain("KB", row, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" ", row, StringComparison.Ordinal);
    }

    private static HistoryViewModel CreateViewModel() =>
        new(new FakeHistoryService(), new FakeLocalization(), null!);

    private static HistorySnapshot CreateSnapshot() =>
        HistorySnapshot.Unavailable(null) with
        {
            IsAvailable = true,
            Error = null,
            Today = new TrafficUsage(1000, 2000),
            Last7Days = new TrafficUsage(7000, 8000),
            DailySeries = new[]
            {
                new DailyUsagePoint(DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), 500, 600),
                new DailyUsagePoint(DateOnly.FromDateTime(DateTime.Today), 1000, 2000),
            },
            TodayHourly = new[]
            {
                new HourlyUsagePoint(DateTime.Today.AddHours(7), DateTime.Today.AddHours(8), 7, 100, 50),
            },
        };

    private sealed class FakeHistoryService : ITrafficHistoryService
    {
        private readonly HistorySnapshot _snapshot = CreateSnapshot();

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