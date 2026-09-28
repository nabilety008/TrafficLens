using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using TrafficLens.Core.Abstractions;
using TrafficLens.WinUI.Infrastructure;
using TrafficLens.Core.Localization;
using TrafficLens.Core.Models;
using TrafficLens.Core.Selection;
using TrafficLens.WinUI.ViewModels;

namespace TrafficLens.WinUI.Tests;

/// <summary>
/// Guards the Connections filter/sort defaults against the blank-ComboBox
/// regression: the option collections are cleared and rebuilt on every culture
/// change, which drops a ComboBox selection to null, so the view model must
/// re-announce its (unchanged) values after the rebuild.
/// </summary>
public sealed class ConnectionsFilterDefaultsTests
{
    [Fact]
    public void FreshViewModel_ShowFilter_FamilyFilter_AndSortKey_HaveExplicitDefaults()
    {
        var vm = CreateViewModel();

        Assert.Equal(ConnectionFilter.All, vm.Filter);
        Assert.Equal(ConnectionFilter.All, vm.FamilyFilter);
        Assert.Equal(ConnectionSortKey.Default, vm.SortKey);

        // The page binds SelectedIndex, so the index defaults must resolve to
        // the first ("All" / "Default") entry of each fixed list.
        Assert.Equal(0, vm.FilterIndex);
        Assert.Equal(0, vm.FamilyFilterIndex);
        Assert.Equal(0, vm.SortIndex);
    }

    [Fact]
    public void SettingIndex_PropagatesToTheEnumState()
    {
        var vm = CreateViewModel();

        vm.FilterIndex = 3;
        Assert.Equal(ConnectionFilter.Tcp, vm.Filter);
        Assert.Equal(3, vm.FilterIndex);

        vm.FilterIndex = 0;
        Assert.Equal(ConnectionFilter.All, vm.Filter);

        vm.FamilyFilterIndex = 2;
        Assert.Equal(ConnectionFilter.Ipv6, vm.FamilyFilter);
        vm.FamilyFilterIndex = 0;

        vm.SortIndex = 1;
        Assert.Equal(ConnectionSortKey.Process, vm.SortKey);
        vm.SortIndex = 0;
        Assert.Equal(ConnectionSortKey.Default, vm.SortKey);
    }

    [Fact]
    public void EnumStateChange_KeepsIndexInSync()
    {
        var vm = CreateViewModel();

        vm.Filter = ConnectionFilter.Udp;
        Assert.Equal(4, vm.FilterIndex);

        vm.SortKey = ConnectionSortKey.Remote;
        Assert.Equal(6, vm.SortIndex);

        vm.FamilyFilter = ConnectionFilter.Ipv4;
        Assert.Equal(1, vm.FamilyFilterIndex);
    }

    [Fact]
    public void EveryDefault_IsPresentInItsOptionList()
    {
        var vm = CreateViewModel();

        Assert.Contains(vm.FilterOptions, o => o.Key == vm.Filter);
        Assert.Contains(vm.FamilyFilterOptions, o => o.Key == vm.FamilyFilter);
        Assert.Contains(vm.SortOptions, o => o.Key == vm.SortKey);
    }

    [Fact]
    public void CultureSwitch_RebuildsOptions_WithoutLosingTheSelections()
    {
        var (vm, localization) = CreateViewModelWithCultureSwitch();

        // The user's chosen selection must survive the rebuild that a culture
        // switch performs, exactly as the fresh defaults do.
        Assert.Contains(vm.FilterOptions, o => o.Key == vm.Filter);
        Assert.Contains(vm.FamilyFilterOptions, o => o.Key == vm.FamilyFilter);
        Assert.Contains(vm.SortOptions, o => o.Key == vm.SortKey);
        Assert.Equal(ConnectionFilter.All, vm.Filter);
        Assert.Equal(ConnectionFilter.All, vm.FamilyFilter);
        Assert.Equal(ConnectionSortKey.Default, vm.SortKey);
    }

    [Fact]
    public void CultureSwitch_ReAnnouncesFilterPropertiesSoBindingsReResolve()
    {
        var (vm, localization) = CreateViewModelWithCultureSwitch();
        var announced = new List<string>();
        vm.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

        localization.SetCulture("fa-IR");

        // The view model marshals the culture change onto its dispatcher queue,
        // so the re-announcement lands asynchronously; the fake localization's
        // CultureChanged is raised synchronously and the VM's RunOnUi posts to
        // the dedicated dispatcher thread. Wait for it, then pump.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3000 && announced.Count < 3)
        {
            Thread.Sleep(50);
        }

        // The rebuild has to raise these, or the ComboBox SelectedValue binding
        // stays pointed at the emptied collection and the visual selection is lost.
        Assert.Contains(nameof(ConnectionsViewModel.Filter), announced);
        Assert.Contains(nameof(ConnectionsViewModel.FamilyFilter), announced);
        Assert.Contains(nameof(ConnectionsViewModel.SortKey), announced);
    }

    [Fact]
    public void ShowList_ContainsExactlyTheFiveShippedChoices()
    {
        var vm = CreateViewModel();

        Assert.Equal(
            new[]
            {
                ConnectionFilter.All,
                ConnectionFilter.Established,
                ConnectionFilter.Listening,
                ConnectionFilter.Tcp,
                ConnectionFilter.Udp
            },
            vm.FilterOptions.Select(o => o.Key));
    }

    private static ConnectionsViewModel CreateViewModel()
    {
        var provider = new FakeConnectionProvider();
        return new ConnectionsViewModel(
            provider,
            new FakeLocalization(),
            new RecordingSettingsService(),
            CreateTestDispatcherQueue(),
            new ProcessIconCache(TestDispatcher.Value));
    }

    private static (ConnectionsViewModel Vm, FakeLocalization Localization) CreateViewModelWithCultureSwitch()
    {
        var localization = new FakeLocalization();
        var vm = new ConnectionsViewModel(
            new FakeConnectionProvider(),
            localization,
            new RecordingSettingsService(),
            TestDispatcher.Value,
            new ProcessIconCache(TestDispatcher.Value));
        return (vm, localization);
    }

    /// <summary>
    /// A real dedicated-thread DispatcherQueue, created once per test assembly:
    /// the view model dereferences it in the constructor (IconReady subscription)
    /// and marshals culture changes through it, so a null stand-in cannot cover
    /// the rebuild path these tests pin. The test host is an unpackaged process,
    /// exactly like the shipped app, so the Windows App SDK runtime is made
    /// resolvable with the bootstrap initializer before any WinUI type activates.
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

    private static DispatcherQueue CreateTestDispatcherQueue() => TestDispatcher.Value;

    private sealed class FakeConnectionProvider : IConnectionProvider
    {
#pragma warning disable CS0067
        public event EventHandler<IReadOnlyList<ConnectionInfo>>? ConnectionsChanged;
#pragma warning restore CS0067

        public string? LastError => null;

        public IReadOnlyList<ConnectionInfo> GetCurrentConnections() => Array.Empty<ConnectionInfo>();

        public Task<IReadOnlyList<ConnectionInfo>> GetActiveConnectionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ConnectionInfo>>(Array.Empty<ConnectionInfo>());

        public void SetPollingEnabled(bool enabled)
        {
        }

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
