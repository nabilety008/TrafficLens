using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;
using TrafficLens.Core.Models;
using TrafficLens.Core.Selection;
using TrafficLens.WinUI.Infrastructure;
using TrafficLens.WinUI.ViewModels;

namespace TrafficLens.WinUI.Tests;

/// <summary>
/// Guards the Applications "Sort by" default against the blank-ComboBox
/// regression. The localized option collection is cleared and rebuilt on every
/// culture change, which drops a SelectedValue-based selection to nothing, so the
/// page binds SelectedIndex against a fixed canonical key order instead.
/// </summary>
public sealed class ApplicationsSortSelectionTests
{
    [Fact]
    public void FreshViewModel_SortIndex_DefaultsToTheCanonicalFirstOption()
    {
        var vm = CreateViewModel().Vm;

        // Index 0 must be the canonical default, not an arbitrary position.
        Assert.Equal(ProcessSortKey.TotalRate, vm.SortKey);
        Assert.Equal(0, vm.SortIndex);
    }

    [Fact]
    public void FreshViewModel_SortOptionList_MatchesTheCanonicalIndexOrder()
    {
        var vm = CreateViewModel().Vm;

        Assert.Equal(
            new[]
            {
                ProcessSortKey.TotalRate,
                ProcessSortKey.DownloadRate,
                ProcessSortKey.UploadRate,
                ProcessSortKey.TotalTransferred,
                ProcessSortKey.Downloaded,
                ProcessSortKey.Uploaded,
                ProcessSortKey.Name
            },
            vm.SortOptions.Select(o => o.Key));
    }

    [Fact]
    public void FreshViewModel_DefaultSelection_IsPresentInTheOptionList()
    {
        var vm = CreateViewModel().Vm;

        // The invariant behind a non-blank ComboBox: the selected key must exist
        // in the bound item list, and the bound index must point at it.
        Assert.Contains(vm.SortOptions, o => o.Key == vm.SortKey);
        Assert.Equal(vm.SortKey, vm.SortOptions[vm.SortIndex].Key);
    }

    [Fact]
    public void FreshViewModel_EveryOptionHasANonEmptyLocalizedLabel()
    {
        var vm = CreateViewModel().Vm;

        Assert.Equal(
            vm.SortOptions.Count,
            vm.SortOptions.Count(o => !string.IsNullOrWhiteSpace(o.Label)));
    }

    [Fact]
    public void SettingIndex_PropagatesToTheEnumState()
    {
        var vm = CreateViewModel().Vm;

        vm.SortIndex = 1;
        Assert.Equal(ProcessSortKey.DownloadRate, vm.SortKey);
        Assert.Equal(1, vm.SortIndex);

        vm.SortIndex = 6;
        Assert.Equal(ProcessSortKey.Name, vm.SortKey);
        Assert.Equal(6, vm.SortIndex);

        vm.SortIndex = 0;
        Assert.Equal(ProcessSortKey.TotalRate, vm.SortKey);
    }

    [Fact]
    public void EnumStateChange_KeepsIndexInSync()
    {
        var vm = CreateViewModel().Vm;

        vm.SortKey = ProcessSortKey.Uploaded;
        Assert.Equal(5, vm.SortIndex);

        vm.SortKey = ProcessSortKey.TotalRate;
        Assert.Equal(0, vm.SortIndex);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(99)]
    public void OutOfRangeIndex_IsIgnored_AndLeavesTheSelectionIntact(int index)
    {
        var vm = CreateViewModel().Vm;
        vm.SortKey = ProcessSortKey.Name;

        // A ComboBox pushes SelectedIndex = -1 while its items are being replaced.
        // That transient value must not corrupt the state or throw.
        vm.SortIndex = index;

        Assert.Equal(ProcessSortKey.Name, vm.SortKey);
        Assert.Equal(6, vm.SortIndex);
    }

    [Fact]
    public void OutOfRangeIndex_DoesNotThrow()
    {
        var vm = CreateViewModel().Vm;

        Assert.Null(Record.Exception(() => vm.SortIndex = -1));
    }

    [Fact]
    public void CultureSwitch_RebuildsOptions_WithoutLosingTheSelection()
    {
        var (vm, localization, _) = CreateViewModel();
        vm.SortKey = ProcessSortKey.Uploaded;

        localization.SetCulture("fa-IR");
        PumpUntil(() => vm.SortOptions.Any(o => o.Label == "fa-IR:SortUploadedLabel"));

        Assert.Equal(ProcessSortKey.Uploaded, vm.SortKey);
        Assert.Equal(5, vm.SortIndex);
        Assert.Contains(vm.SortOptions, o => o.Key == vm.SortKey);
        Assert.Equal(vm.SortKey, vm.SortOptions[vm.SortIndex].Key);
    }

    [Fact]
    public void CultureSwitch_RelabelsTheOptionsInTheNewCulture()
    {
        var (vm, localization, _) = CreateViewModel();

        localization.SetCulture("fa-IR");
        PumpUntil(() => vm.SortOptions.Any(o => o.Label == "fa-IR:SortTotalRateLabel"));

        // Presentation is localized; the semantic key order is untouched.
        Assert.All(vm.SortOptions, o => Assert.StartsWith("fa-IR:", o.Label));
        Assert.Equal(ProcessSortKey.TotalRate, vm.SortOptions[0].Key);
        Assert.Equal(0, vm.SortIndex);
    }

    [Fact]
    public void CultureSwitch_ReAnnouncesSortIndexSoTheBindingReResolves()
    {
        var (vm, localization, _) = CreateViewModel();
        var announced = new List<string>();
        vm.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

        localization.SetCulture("fa-IR");

        // The rebuild is marshalled onto the dispatcher queue, so it lands
        // asynchronously. Wait for it, then assert.
        PumpUntil(() => announced.Contains(nameof(ApplicationsViewModel.SortIndex)));

        // Without these the ComboBox SelectedIndex binding stays pointed at the
        // emptied collection and the visual selection is lost.
        Assert.Contains(nameof(ApplicationsViewModel.SortIndex), announced);
        Assert.Contains(nameof(ApplicationsViewModel.SortKey), announced);
    }

    [Fact]
    public void CultureSwitchRoundTrip_FaIr_To_EnUs_And_Back_PreservesTheSelection()
    {
        var (vm, localization, _) = CreateViewModel();
        vm.SortKey = ProcessSortKey.DownloadRate;

        localization.SetCulture("fa-IR");
        PumpUntil(() => vm.SortOptions.Any(o => o.Label == "fa-IR:SortDownloadRateLabel"));
        Assert.Equal(ProcessSortKey.DownloadRate, vm.SortKey);
        Assert.Equal(1, vm.SortIndex);

        localization.SetCulture("en-US");
        PumpUntil(() => vm.SortOptions.Any(o => o.Label == "en-US:SortDownloadRateLabel"));
        Assert.Equal(ProcessSortKey.DownloadRate, vm.SortKey);
        Assert.Equal(1, vm.SortIndex);

        localization.SetCulture("fa-IR");
        PumpUntil(() => vm.SortOptions.Any(o => o.Label == "fa-IR:SortDownloadRateLabel"));
        Assert.Equal(ProcessSortKey.DownloadRate, vm.SortKey);
        Assert.Equal(1, vm.SortIndex);
        Assert.Equal(vm.SortKey, vm.SortOptions[vm.SortIndex].Key);
    }

    [Fact]
    public void SetActiveFalseThenTrue_PreservesTheSelection()
    {
        // Navigating away calls SetActive(false); returning calls SetActive(true).
        var (vm, _, _) = CreateViewModel();
        vm.SortKey = ProcessSortKey.TotalTransferred;

        vm.SetActive(false);
        vm.SetActive(true);

        Assert.Equal(ProcessSortKey.TotalTransferred, vm.SortKey);
        Assert.Equal(3, vm.SortIndex);
        Assert.Contains(vm.SortOptions, o => o.Key == vm.SortKey);
    }

    [Fact]
    public void SetActiveFalseThenTrue_AfterCultureSwitch_StillPreservesTheSelection()
    {
        var (vm, localization, _) = CreateViewModel();
        vm.SortKey = ProcessSortKey.Name;

        localization.SetCulture("fa-IR");
        PumpUntil(() => vm.SortOptions.Any(o => o.Label == "fa-IR:SortNameLabel"));

        vm.SetActive(false);
        vm.SetActive(true);

        Assert.Equal(ProcessSortKey.Name, vm.SortKey);
        Assert.Equal(6, vm.SortIndex);
        Assert.Equal(vm.SortKey, vm.SortOptions[vm.SortIndex].Key);
    }

    [Fact]
    public void RawByteSorting_RemainsNumeric_AndNotStringOrdered()
    {
        var (vm, _, collector) = CreateViewModel();

        vm.SortKey = ProcessSortKey.TotalTransferred;
        collector.RaiseSamples(new[]
        {
            Sample("small", 1, downloadBytes: 500, uploadBytes: 499),
            Sample("large", 2, downloadBytes: 900_000_000, uploadBytes: 100_000_000),
            Sample("medium", 3, downloadBytes: 4_000, uploadBytes: 1_000)
        });
        PumpUntil(() => vm.Processes.Count == 3);

        // 1,000,000,000 must sort above 5,000 and 999. A lexicographic sort on a
        // formatted display string would invert this.
        Assert.Equal(
            new[] { "large", "medium", "small" },
            vm.Processes.Select(p => p.Name));
    }

    [Fact]
    public void RateSorting_RemainsNumeric_Descending()
    {
        var (vm, _, collector) = CreateViewModel();

        vm.SortKey = ProcessSortKey.TotalRate;
        collector.RaiseSamples(new[]
        {
            Sample("low", 1, downloadRate: 600, uploadRate: 400),
            Sample("high", 2, downloadRate: 700_000, uploadRate: 200_000),
            Sample("mid", 3, downloadRate: 40_000, uploadRate: 10_000)
        });
        PumpUntil(() => vm.Processes.Count == 3);

        Assert.Equal(
            new[] { "high", "mid", "low" },
            vm.Processes.Select(p => p.Name));
    }

    [Fact]
    public void NameSorting_StillSortsByName()
    {
        var (vm, _, collector) = CreateViewModel();

        vm.SortKey = ProcessSortKey.Name;
        collector.RaiseSamples(new[]
        {
            Sample("charlie", 1, downloadBytes: 1, uploadBytes: 1),
            Sample("alpha", 2, downloadBytes: 1, uploadBytes: 1),
            Sample("bravo", 3, downloadBytes: 1, uploadBytes: 1)
        });
        PumpUntil(() => vm.Processes.Count == 3);

        Assert.Equal(
            new[] { "alpha", "bravo", "charlie" },
            vm.Processes.Select(p => p.Name));
    }

    [Fact]
    public void SelectingAnIndex_ActuallyReordersTheList()
    {
        // Ties the UI selection to the underlying sort state end to end.
        var (vm, _, collector) = CreateViewModel();
        collector.RaiseSamples(new[]
        {
            Sample("a", 1, downloadBytes: 6, uploadBytes: 4),
            Sample("b", 2, downloadBytes: 20, uploadBytes: 10),
            Sample("c", 3, downloadBytes: 14, uploadBytes: 6)
        });
        PumpUntil(() => vm.Processes.Count == 3);

        vm.SortIndex = 3; // Total transferred
        Assert.Equal(
            new[] { "b", "c", "a" },
            vm.Processes.Select(p => p.Name));

        vm.SortIndex = 6; // Name
        Assert.Equal(
            new[] { "a", "b", "c" },
            vm.Processes.Select(p => p.Name));
    }

    /// <summary>
    /// The view model marshals collector and culture notifications through its
    /// dispatcher queue, so a test that observes the result has to let the queue
    /// drain before asserting.
    /// </summary>
    private static void PumpUntil(Func<bool> condition)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3000 && !condition())
        {
            Thread.Sleep(25);
        }
    }

    private static (ApplicationsViewModel Vm, FakeLocalization Localization, FakeProcessCollector Collector) CreateViewModel()
    {
        var localization = new FakeLocalization();
        var collector = new FakeProcessCollector();
        var vm = new ApplicationsViewModel(
            collector,
            localization,
            TestDispatcher.Value,
            new ProcessIconCache(TestDispatcher.Value));
        return (vm, localization, collector);
    }

    /// <summary>
    /// A real dedicated-thread DispatcherQueue, created once per test assembly:
    /// the view model dereferences it in the constructor and marshals collector
    /// and culture notifications through it, so a null stand-in cannot cover the
    /// paths these tests pin. The test host is an unpackaged process, exactly
    /// like the shipped app, so the Windows App SDK runtime is made resolvable
    /// with the bootstrap initializer before any WinUI type activates.
    /// </summary>
    private static readonly Lazy<DispatcherQueue> TestDispatcher = new(() =>
    {
        var rc = MddBootstrapInitialize2(0x00020000, null, 0, 0);
        if (rc != 0)
        {
            throw new InvalidOperationException(
                $"Windows App SDK bootstrap failed: 0x{rc:X8}");
        }

        var controller = DispatcherQueueController.CreateOnDedicatedThread();
        return controller.DispatcherQueue;
    });

    [DllImport("Microsoft.WindowsAppRuntime.Bootstrap.dll", SetLastError = false)]
    private static extern int MddBootstrapInitialize2(
        uint majorMinorVersion,
        string? versionTag,
        ulong minimumVersion,
        uint options);

    private static ProcessTrafficSample Sample(
        string name,
        int pid,
        long downloadBytes = 0,
        long uploadBytes = 0,
        double downloadRate = 0,
        double uploadRate = 0) =>
        new(
            pid,
            ProcessStartTimeUtcTicks: 1,
            name,
            $@"C:\{name}.exe",
            IconAvailable: false,
            downloadBytes,
            uploadBytes,
            downloadRate,
            uploadRate,
            Timestamp: DateTime.UnixEpoch);

    private sealed class FakeProcessCollector : IProcessTrafficCollector
    {
#pragma warning disable CS0067
        public event EventHandler? StatusChanged;
        public event EventHandler<IReadOnlyList<ProcessTrafficSample>>? SamplesReady;
#pragma warning restore CS0067

        public ProcessTrafficCollectorStatus Status => ProcessTrafficCollectorStatus.Running;

        public string? LastError => null;

        public IReadOnlyList<ProcessTrafficSample> GetCurrentSamples() => Array.Empty<ProcessTrafficSample>();

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }

        public void RaiseSamples(IReadOnlyList<ProcessTrafficSample> samples) =>
            SamplesReady?.Invoke(this, samples);
    }

    private sealed class FakeLocalization : ILocalizationService
    {
        public CultureInfo CurrentCulture { get; private set; } = CultureInfo.InvariantCulture;

        public event EventHandler? CultureChanged;

        public string this[string key] => $"{CurrentCulture.Name}:{key}";

        public bool IsRightToLeft =>
            CurrentCulture.Name.StartsWith("fa", StringComparison.OrdinalIgnoreCase);

        public void SetCulture(string cultureName)
        {
            CurrentCulture = new CultureInfo(cultureName);
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }

        public string GetString(string key, string? cultureName = null) => this[key];
    }
}