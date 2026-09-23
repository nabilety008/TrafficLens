using System.Threading;

namespace TrafficLens.WinUI.Infrastructure;

public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _activationEvent;
    private readonly bool _ownsMutex;
    private bool _disposed;

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle? activationEvent, bool ownsMutex)
    {
        _mutex = mutex;
        _activationEvent = activationEvent;
        _ownsMutex = ownsMutex;
    }

    public bool IsPrimary { get; private init; }

    public static SingleInstanceGuard TryAcquire(string mutexName, string activationEventName)
    {
        try
        {
            var mutex = new Mutex(initiallyOwned: false, mutexName, out _);

            bool ownsMutex;
            try
            {
                ownsMutex = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                ownsMutex = true;
            }

            if (!ownsMutex)
            {
                SignalExistingInstance(activationEventName);
                return new SingleInstanceGuard(mutex, null, ownsMutex: false) { IsPrimary = false };
            }

            EventWaitHandle? activationEvent = null;
            try
            {
                activationEvent = new EventWaitHandle(
                    initialState: false,
                    EventResetMode.AutoReset,
                    activationEventName,
                    out _);
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }

            return new SingleInstanceGuard(mutex, activationEvent, ownsMutex: true) { IsPrimary = true };
        }
        catch (UnauthorizedAccessException)
        {
            return new SingleInstanceGuard(new Mutex(false), null, ownsMutex: false) { IsPrimary = true };
        }
        catch (PlatformNotSupportedException)
        {
            return new SingleInstanceGuard(new Mutex(false), null, ownsMutex: false) { IsPrimary = true };
        }
    }

    public void SignalActivation()
    {
        if (_activationEvent is null)
        {
            return;
        }

        try
        {
            _activationEvent.Set();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public IDisposable StartActivationWatcher(Action onActivationRequested)
    {
        if (!IsPrimary)
        {
            throw new InvalidOperationException("Only the primary instance watches for activation requests.");
        }

        var cts = new CancellationTokenSource();
        var thread = new Thread(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                if (_activationEvent is null)
                {
                    break;
                }

                try
                {
                    _activationEvent.WaitOne();
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (!cts.IsCancellationRequested)
                {
                    onActivationRequested();
                }
            }
        })
        {
            IsBackground = true,
            Name = "TrafficLensWinUIActivationWatcher"
        };
        thread.Start();

        return new ActivationWatch(cts, thread, _activationEvent);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _activationEvent?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }

        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }

        _mutex.Dispose();
    }

    private static void SignalExistingInstance(string activationEventName)
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(activationEventName, out var existing))
            {
                using (existing)
                {
                    existing.Set();
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }

    private sealed class ActivationWatch : IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private readonly Thread _thread;
        private readonly EventWaitHandle? _event;

        public ActivationWatch(CancellationTokenSource cts, Thread thread, EventWaitHandle? singleEvent)
        {
            _cts = cts;
            _thread = thread;
            _event = singleEvent;
        }

        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                _event?.Set();
            }
            catch (ObjectDisposedException)
            {
            }

            _thread.Join(TimeSpan.FromSeconds(3));
            _cts.Dispose();
        }
    }
}
