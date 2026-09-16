using System;
using System.Threading;

namespace TrafficLens.App.Infrastructure;

/// <summary>
/// Ensures at most one TrafficLens instance runs per user session.
///
/// The first instance wins a named mutex. A later launch observes that the
/// mutex is held, signals a named auto-reset event so the primary can activate
/// its main window, and reports itself as non-primary (the caller exits).
///
/// The mutex and event are session-scoped named kernel objects (no "Global\"
/// prefix), so behaviour is isolated per interactive session.
/// </summary>
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

    /// <summary>
    /// Attempts to become the primary instance. When another instance holds
    /// the mutex, the activation event is signalled and the returned guard
    /// reports <see cref="IsPrimary"/> equal to false.
    /// </summary>
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
                // Event is blocked (hardened session ACL) - activation is best
                // effort only; the guard still enforces single-instance.
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }

            return new SingleInstanceGuard(mutex, activationEvent, ownsMutex: true) { IsPrimary = true };
        }
        catch (UnauthorizedAccessException)
        {
            // Not permitted to create the mutex (hostile/hardened session).
            // Degrade to primary-only so the app still runs.
            return new SingleInstanceGuard(new Mutex(false), null, ownsMutex: false) { IsPrimary = true };
        }
        catch (PlatformNotSupportedException)
        {
            return new SingleInstanceGuard(new Mutex(false), null, ownsMutex: false) { IsPrimary = true };
        }
    }

    /// <summary>
    /// Signals the primary instance to activate its main window. Only
    /// meaningful when <see cref="IsPrimary"/> is false.
    /// </summary>
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

    /// <summary>
    /// Starts a background thread that raises <paramref name="onActivationRequested"/>
    /// whenever another instance signals activation. Only valid for the primary
    /// instance. The returned handle stops the watcher when disposed.
    /// </summary>
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
            Name = "TrafficLensActivationWatcher"
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