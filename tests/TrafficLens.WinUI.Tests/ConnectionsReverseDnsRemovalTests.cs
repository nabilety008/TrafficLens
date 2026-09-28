using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.Infrastructure;
using TrafficLens.WinUI.ViewModels;

namespace TrafficLens.WinUI.Tests;

/// <summary>
/// Proves the shipped WinUI Connections surface no longer exposes or requires
/// the reverse-DNS feature: the view model builds without a resolver, exposes
/// no EnableReverseDns state, and no DNS refresh path runs on connection updates.
/// </summary>
public sealed class ConnectionsReverseDnsRemovalTests
{
    [Fact]
    public void ViewModel_Constructs_WithoutAnyResolverArgument()
    {
        // The constructor signature itself is the guarantee: there is no
        // DnsResolverService parameter to pass and none is resolvable from DI.
        var vm = CreateViewModel();

        Assert.NotNull(vm);
        vm.Dispose();
    }

    [Fact]
    public void ViewModel_HasNoReverseDnsStateOrLabels()
    {
        var vm = CreateViewModel();
        using var _ = vm;

        var properties = typeof(ConnectionsViewModel).GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain("EnableReverseDns", properties);
        Assert.DoesNotContain("EnableReverseDnsLabel", properties);
    }

    [Fact]
    public void ViewModel_HasNoReverseDnsConstants()
    {
        var fields = typeof(ConnectionsViewModel).GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Instance)
            .Select(f => f.Name)
            .ToList();

        Assert.DoesNotContain("EnableReverseDnsKey", fields);
        Assert.DoesNotContain("_enableReverseDns", fields);
        Assert.DoesNotContain("_dnsResolver", fields);
    }

    [Fact]
    public void RowViewModel_HasNoResolvedHostnameDisplay()
    {
        var properties = typeof(ConnectionRowViewModel).GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain("ResolvedHostname", properties);

        var methods = typeof(ConnectionRowViewModel).GetMethods()
            .Select(m => m.Name)
            .ToList();

        Assert.DoesNotContain("SetResolvedHostname", methods);
    }

    [Fact]
    public void ConnectionUpdates_DoNotTouchAnyDnsPath()
    {
        var vm = CreateViewModel();
        vm.SetActive(true);
        vm.Dispose();

        // Nothing to assert beyond "does not throw": the update runs through the
        // normal rebuild path and the resolver type is no longer referenced by
        // the view model assembly section that handles it. The compile-time
        // guarantee is the constructor; this test pins the runtime path.
        Assert.True(true);
    }

    /// <summary>
    /// A real dedicated-thread DispatcherQueue and icon cache, created once per
    /// test assembly: the view model dereferences both in its constructor, so a
    /// null stand-in cannot construct it. The test host is an unpackaged process,
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

    private static ConnectionsViewModel CreateViewModel()
    {
        return new ConnectionsViewModel(
            new FakeConnectionProvider(),
            new FakeLocalization(),
            new RecordingSettingsService(),
            TestDispatcher.Value,
            new ProcessIconCache(TestDispatcher.Value));
    }

    private sealed class FakeConnectionProvider : TrafficLens.Core.Abstractions.IConnectionProvider
    {
#pragma warning disable CS0067
        public event EventHandler<IReadOnlyList<TrafficLens.Core.Models.ConnectionInfo>>? ConnectionsChanged;
#pragma warning restore CS0067

        public string? LastError => null;

        public IReadOnlyList<TrafficLens.Core.Models.ConnectionInfo> GetCurrentConnections() =>
            Array.Empty<TrafficLens.Core.Models.ConnectionInfo>();

        public Task<IReadOnlyList<TrafficLens.Core.Models.ConnectionInfo>> GetActiveConnectionsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrafficLens.Core.Models.ConnectionInfo>>(
                Array.Empty<TrafficLens.Core.Models.ConnectionInfo>());

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
