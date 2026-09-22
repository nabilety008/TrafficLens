using TrafficLens.App.Services;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Alerts;
using TrafficLens.Core.History;
using TrafficLens.Core.Models;

namespace TrafficLens.App.Tests;

internal sealed class FakeCollector : INetworkTrafficCollector
{
    private readonly List<NetworkSpeedSample> _samples = new();

    #pragma warning disable CS0067
    public event EventHandler<NetworkCounterSample>? CounterSampleReady;
#pragma warning restore CS0067

    public event EventHandler<NetworkSpeedSample>? SpeedSampleReady;

    public event EventHandler? NetworkChanged;

    public IReadOnlyList<NetworkCounterSample> GetCurrentCounterSamples() => Array.Empty<NetworkCounterSample>();

    public IReadOnlyList<NetworkSpeedSample> GetCurrentSamples() => _samples;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;

    public void Dispose()
    {
    }

    public void SetSamples(params NetworkSpeedSample[] samples)
    {
        _samples.Clear();
        _samples.AddRange(samples);
        foreach (var sample in samples)
        {
            SpeedSampleReady?.Invoke(this, sample);
        }
    }

    public void RaiseSample(NetworkSpeedSample sample)
    {
        var existing = _samples.FindIndex(s => s.AdapterId.Equals(sample.AdapterId, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            _samples[existing] = sample;
        }
        else
        {
            _samples.Add(sample);
        }

        SpeedSampleReady?.Invoke(this, sample);
    }

    public void RaiseNetworkChanged() => NetworkChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class FakeAdapterProvider : INetworkAdapterProvider
{
    private IReadOnlyList<NetworkAdapterInfo> _adapters = Array.Empty<NetworkAdapterInfo>();

    public event EventHandler? AdaptersChanged;

    public IReadOnlyList<NetworkAdapterInfo> GetAdapters() => _adapters;

    public NetworkAdapterInfo? GetDefaultAdapter() => _adapters.FirstOrDefault(a => a.IsDefault);

    public void SetAdapters(IReadOnlyList<NetworkAdapterInfo> adapters)
    {
        _adapters = adapters;
        AdaptersChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class FakeProcessCollector : IProcessTrafficCollector
{
    private readonly List<ProcessTrafficSample> _samples = new();

    public ProcessTrafficCollectorStatus Status { get; private set; } = ProcessTrafficCollectorStatus.Running;

    public string? LastError { get; private set; }

    public int StartCalls { get; private set; }

    public event EventHandler<IReadOnlyList<ProcessTrafficSample>>? SamplesReady;

    public event EventHandler? StatusChanged;

    public IReadOnlyList<ProcessTrafficSample> GetCurrentSamples() => _samples;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        StartCalls++;
        SetStatusInternal(ProcessTrafficCollectorStatus.Running, null);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        SetStatusInternal(ProcessTrafficCollectorStatus.Stopped, null);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }

    public void SetStatus(ProcessTrafficCollectorStatus status, string? error = null) =>
        SetStatusInternal(status, error);

    public void PublishSamples(params ProcessTrafficSample[] samples)
    {
        _samples.Clear();
        _samples.AddRange(samples);
        SamplesReady?.Invoke(this, samples.ToArray());
    }

    private void SetStatusInternal(ProcessTrafficCollectorStatus status, string? error)
    {
        Status = status;
        LastError = error;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class FakeConnectionProvider : IConnectionProvider
{
    private readonly List<ConnectionInfo> _connections = new();

    public string? LastError { get; private set; }

    public event EventHandler<IReadOnlyList<ConnectionInfo>>? ConnectionsChanged;

    public IReadOnlyList<ConnectionInfo> GetCurrentConnections() => _connections.ToArray();

    public Task<IReadOnlyList<ConnectionInfo>> GetActiveConnectionsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ConnectionInfo>>(_connections.ToArray());

public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;

    public void SetPollingEnabled(bool enabled)
    {
    }

    public void Dispose()
    {
    }

    public void Publish(params ConnectionInfo[] connections)
    {
        _connections.Clear();
        _connections.AddRange(connections);
        ConnectionsChanged?.Invoke(this, connections.ToArray());
    }

    public void SetError(string? error)
    {
        LastError = error;
        ConnectionsChanged?.Invoke(this, GetCurrentConnections());
    }
}
internal sealed class FakeSettingsService : ISettingsService
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public string Language { get; set; } = "en-US";

    public bool SettingsFileExisted { get; set; } = true;

    public int SaveCalls { get; private set; }

    public string Get(string key, string defaultValue) =>
        _values.TryGetValue(key, out var value) ? value : defaultValue;

    public void Set(string key, string value) => _values[key] = value;

    public void Save() => SaveCalls++;
}

internal sealed class FakeHistoryService : ITrafficHistoryService
{
    public event EventHandler? HistoryChanged;

    public bool IsAvailable { get; set; } = true;

    public string? LastError { get; set; }

    public HistorySnapshot Snapshot { get; set; } = HistorySnapshot.Unavailable(null);

    public HistorySnapshot GetSnapshot() => Snapshot;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;

    public void Dispose()
    {
    }

    public void RaiseChanged() => HistoryChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class FakeAlertService : IAlertService
{
    private readonly List<AlertEvent> _recent = new();

    public int RefreshCalls { get; private set; }

    public int DisposeCalls { get; private set; }

    public event EventHandler<AlertRaisedEventArgs>? AlertRaised;

    public event EventHandler? ConfigChanged;

    public IReadOnlyList<AlertEvent> RecentAlerts => _recent.ToArray();

    public AlertConfig CurrentConfig { get; set; } = AlertConfig.Default();

    public void RefreshConfig()
    {
        RefreshCalls++;
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => DisposeCalls++;

    public void Raise(AlertEvent alert)
    {
        _recent.Add(alert);
        AlertRaised?.Invoke(this, new AlertRaisedEventArgs(alert));
    }
}

internal sealed class FakeTrayService : ISystemTrayService
{
    public int ShowCalls { get; private set; }

    public int DisposeCalls { get; private set; }

    public int NoticeCalls { get; private set; }

    public int AlertCalls { get; private set; }

    public event EventHandler? OpenRequested;

    public event EventHandler? ExitRequested;

    public void Show() => ShowCalls++;

    public void ShowFirstCloseToTrayNotice() => NoticeCalls++;

    public void ShowAlert(string title, string message) => AlertCalls++;

    public void Dispose() => DisposeCalls++;

    public void RaiseOpen() => OpenRequested?.Invoke(this, EventArgs.Empty);

    public void RaiseExit() => ExitRequested?.Invoke(this, EventArgs.Empty);
}

internal sealed class FakeFloatingWidgetService : IFloatingWidgetService
{
    public bool IsVisible { get; private set; }

    public bool IsAlwaysOnTop { get; private set; } = true;

    public int ShowCalls { get; private set; }

    public int HideCalls { get; private set; }

    public int ToggleCalls { get; private set; }

    public int ToggleAlwaysOnTopCalls { get; private set; }

    public int SetAlwaysOnTopCalls { get; private set; }

    public int RestoreCalls { get; private set; }

    public int DisposeCalls { get; private set; }

    public event EventHandler? IsVisibleChanged;

    public event EventHandler<bool>? AlwaysOnTopChanged;

    public void Show()
    {
        ShowCalls++;
        SetVisible(true);
    }

    public void Hide()
    {
        HideCalls++;
        SetVisible(false);
    }

    public void Toggle()
    {
        ToggleCalls++;
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

public void ToggleAlwaysOnTop()
    {
        ToggleAlwaysOnTopCalls++;
        IsAlwaysOnTop = !IsAlwaysOnTop;
        AlwaysOnTopChanged?.Invoke(this, IsAlwaysOnTop);
    }

    public void SetAlwaysOnTop(bool alwaysOnTop)
    {
        SetAlwaysOnTopCalls++;
        IsAlwaysOnTop = alwaysOnTop;
        AlwaysOnTopChanged?.Invoke(this, IsAlwaysOnTop);
    }

    public void RestoreIfEnabled() => RestoreCalls++;

    public void Dispose() => DisposeCalls++;

private void SetVisible(bool visible)
    {
        IsVisible = visible;
        IsVisibleChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class FakeStartupRegistrationService : IStartupRegistrationService
{
    public bool Registered { get; set; }

    public int EnableCalls { get; private set; }

    public int DisableCalls { get; private set; }

    public bool? LastStartMinimized { get; private set; }

    public bool IsRegistered() => Registered;

    public void Enable(bool startMinimized)
    {
        EnableCalls++;
        LastStartMinimized = startMinimized;
        Registered = true;
    }

    public void Disable()
    {
        DisableCalls++;
        Registered = false;
    }
}
