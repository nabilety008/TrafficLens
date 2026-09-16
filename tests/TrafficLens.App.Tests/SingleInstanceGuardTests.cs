using System.Threading;
using TrafficLens.App.Infrastructure;

namespace TrafficLens.App.Tests;

public sealed class SingleInstanceGuardTests
{
    [Fact]
    public void FirstAcquirer_IsPrimary()
    {
        var name = UniqueName();

        using var guard = SingleInstanceGuard.TryAcquire(name, name + "_act");

        Assert.True(guard.IsPrimary);
    }

    [Fact]
    public void SecondAcquirer_IsNotPrimary()
    {
        var name = UniqueName();
        var activationName = name + "_act";

        using var primCtx = StartPrimaryOnBackground(name, activationName);
        using var secondary = SingleInstanceGuard.TryAcquire(name, activationName);

        Assert.False(secondary.IsPrimary);
    }

    [Fact]
    public void SecondAcquirer_SignalsTheActivationEvent()
    {
        var name = UniqueName();
        var activationName = name + "_act";

        using var primCtx = StartPrimaryOnBackground(name, activationName);
        using var secondary = SingleInstanceGuard.TryAcquire(name, activationName);
        Assert.False(secondary.IsPrimary);

        using var probe = new EventWaitHandle(false, EventResetMode.AutoReset, activationName, out _);
        Assert.True(probe.WaitOne(0), "activation event was not signalled by the second instance");
    }

    [Fact]
    public void AfterPrimaryDisposes_NewAcquirer_BecomesPrimary()
    {
        var name = UniqueName();
        var activationName = name + "_act";

        SingleInstanceGuard? primary = null;
        var primaryThread = new Thread(() =>
        {
            primary = SingleInstanceGuard.TryAcquire(name, activationName);
        })
        {
            IsBackground = true,
            Name = "GuardTestPrimary-Lifecycle"
        };
        primaryThread.Start();
        primaryThread.Join(TimeSpan.FromSeconds(5));

        using var _ = primary!;
        Assert.True(primary!.IsPrimary);
        primary.Dispose();

        using var next = SingleInstanceGuard.TryAcquire(name, activationName);

        Assert.True(next.IsPrimary);
    }

    [Fact]
    public void StartActivationWatcher_RaisesCallbackOnSignal()
    {
        var name = UniqueName();
        var activationName = name + "_act";
        using var guard = SingleInstanceGuard.TryAcquire(name, activationName);
        using var signalled = new ManualResetEventSlim();
        var calls = 0;

        using (guard.StartActivationWatcher(() =>
        {
            Interlocked.Increment(ref calls);
            signalled.Set();
        }))
        {
            guard.SignalActivation();

            Assert.True(signalled.Wait(TimeSpan.FromSeconds(10)), "activation callback was not raised");
        }

        Assert.True(calls > 0);
    }

    [Fact]
    public void StartActivationWatcher_OnSecondary_Throws()
    {
        var name = UniqueName();
        var activationName = name + "_act";

        using var primCtx = StartPrimaryOnBackground(name, activationName);
        using var secondary = SingleInstanceGuard.TryAcquire(name, activationName);

        Assert.Throws<InvalidOperationException>(() => secondary.StartActivationWatcher(() => { }));
    }

    private static PrimaryContext StartPrimaryOnBackground(string name, string activationName)
    {
        var ready = new ManualResetEventSlim(false);
        var stop = new ManualResetEventSlim(false);
        SingleInstanceGuard? guard = null;

        var thread = new Thread(() =>
        {
            guard = SingleInstanceGuard.TryAcquire(name, activationName);
            ready.Set();
            stop.Wait();
            guard?.Dispose();
        })
        {
            IsBackground = true,
            Name = "GuardTestPrimary"
        };
        thread.Start();
        ready.Wait();

        return new PrimaryContext(guard!, thread, stop);
    }

    private static string UniqueName() => "tl_guard_" + Guid.NewGuid().ToString("N");

    private sealed class PrimaryContext : IDisposable
    {
        private readonly SingleInstanceGuard _guard;
        private readonly Thread _thread;
        private readonly ManualResetEventSlim _stop;

        public PrimaryContext(SingleInstanceGuard guard, Thread thread, ManualResetEventSlim stop)
        {
            _guard = guard;
            _thread = thread;
            _stop = stop;
        }

        public void Dispose()
        {
            _stop.Set();
            _thread.Join(TimeSpan.FromSeconds(3));
            _stop.Dispose();
        }
    }
}