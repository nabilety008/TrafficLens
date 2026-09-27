using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Windows.Foundation;
using Windows.Graphics;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;

namespace TrafficLens.WinUI.Services;

public sealed class FloatingWidgetService : IFloatingWidgetService
{
    private readonly ISettingsService _settings;
    private readonly IServiceProvider _services;
    private readonly ILocalizationService _localization;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly WidgetEnabledState _enabledState;

    private Views.FloatingWidgetWindow? _window;
    private ViewModels.FloatingWidgetViewModel? _viewModel;
    private bool _isVisible;

    public FloatingWidgetService(
        ISettingsService settings,
        IServiceProvider services,
        ILocalizationService localization,
        DispatcherQueue dispatcherQueue)
    {
        _settings = settings;
        _services = services;
        _localization = localization;
        _dispatcherQueue = dispatcherQueue;
        _enabledState = new WidgetEnabledState(settings);
        _enabledState.Changed += OnEnabledStateChanged;
    }

    public event EventHandler? IsVisibleChanged;

    public event EventHandler<bool>? AlwaysOnTopChanged;

    public event EventHandler<bool>? EnabledChanged;

    public bool IsEnabled => _enabledState.IsEnabled;

    public bool IsAlwaysOnTop => GetAlwaysOnTop();

    public bool IsVisible
    {
        get => _isVisible;
        private set
        {
            if (_isVisible == value)
            {
                return;
            }

            _isVisible = value;
            IsVisibleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// The single deterministic path for turning the widget on or off. It persists
    /// the state first, then shows or hides the window, so nothing that observes the
    /// change can be interrupted by the window work.
    /// </summary>
    public void SetEnabled(bool enabled) => RunOnUi(() => ApplyEnabled(enabled));

    public void Show() => SetEnabled(true);

    public void Hide() => SetEnabled(false);

    public void Toggle() => SetEnabled(!IsEnabled);

    /// <summary>
    /// Recreates the widget on start-up only when the persisted setting says it was
    /// enabled. The window is shown directly rather than through
    /// <see cref="SetEnabled"/>, because the state is already true after the reload
    /// and the idempotent state owner would otherwise treat the call as a no-op and
    /// never put the window back on screen.
    /// </summary>
    public void RestoreIfEnabled()
    {
        _enabledState.Reload();
        if (IsEnabled)
        {
            RunOnUi(ShowWindow);
        }
    }

    public void ToggleAlwaysOnTop()
    {
        ApplyAlwaysOnTop(!GetAlwaysOnTop(), notify: true);
    }

    public void SetAlwaysOnTop(bool alwaysOnTop)
    {
        ApplyAlwaysOnTop(alwaysOnTop, notify: true);
    }

    public void Dispose()
    {
        _enabledState.Changed -= OnEnabledStateChanged;
        RunOnUi(() =>
        {
            if (_window is not null)
            {
                _window.UserCloseRequested -= OnUserCloseRequested;
                SavePosition();
                _window.CloseWidget();
                _window = null;
            }

            if (_viewModel is not null)
            {
                _viewModel.CloseRequested -= OnCloseRequested;
                _viewModel.PinStateChanged -= OnPinStateChanged;
                _viewModel.Dispose();
                _viewModel = null;
            }
        });
    }

    private void ApplyEnabled(bool enabled)
    {
        if (!_enabledState.SetEnabled(enabled))
        {
            // Already in the requested state. Re-applying visibility would be
            // harmless but pointless, and skipping it keeps repeated toggles from
            // re-running the window work.
            return;
        }

        if (enabled)
        {
            ShowWindow();
        }
        else
        {
            HideWindow();
        }
    }

    private void ShowWindow()
    {
        EnsureCreated();
        if (_window is null)
        {
            return;
        }

        RestorePosition();
        ApplyAlwaysOnTop(GetAlwaysOnTop(), notify: false);
        _window.ShowWidget();
        IsVisible = true;
    }

    private void HideWindow()
    {
        if (_window is not null)
        {
            SavePosition();
            _window.HideWidget();
            _viewModel?.Suspend();
        }

        IsVisible = false;
    }

    private void OnEnabledStateChanged(object? sender, bool enabled) =>
        EnabledChanged?.Invoke(this, enabled);

    private void EnsureCreated()
    {
        if (_window is not null)
        {
            _viewModel?.Resume();
            return;
        }

        _viewModel = CreateWidgetViewModel();
        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.PinStateChanged += OnPinStateChanged;

        _window = new Views.FloatingWidgetWindow(_viewModel, _localization);
        _window.UserCloseRequested += OnUserCloseRequested;
    }

    private ViewModels.FloatingWidgetViewModel CreateWidgetViewModel() =>
        new(
            _services.GetRequiredService<INetworkTrafficCollector>(),
            _services.GetRequiredService<INetworkAdapterProvider>(),
            _localization,
            _dispatcherQueue);

    private void OnCloseRequested(object? sender, EventArgs e) => SetEnabled(false);

    /// <summary>
    /// The widget's own close button means "disable the widget". It is routed
    /// through the same <see cref="SetEnabled"/> path as the Settings switches, so
    /// the setting is persisted and both switches follow. The window cancels its
    /// own close, so nothing here can reach MainWindow, the tray or the collectors.
    /// </summary>
    private void OnUserCloseRequested(object? sender, EventArgs e) => SetEnabled(false);

    private void OnPinStateChanged(object? sender, bool isPinned) =>
        ApplyAlwaysOnTop(isPinned, notify: true);

    private void ApplyAlwaysOnTop(bool alwaysOnTop, bool notify)
    {
        if (_window is not null)
        {
            _window.SetAlwaysOnTop(alwaysOnTop);
            _viewModel!.IsPinned = alwaysOnTop;
        }

        _settings.Set(FloatingWidgetSettings.AlwaysOnTopKey, alwaysOnTop.ToString());
        _settings.Save();

        if (notify)
        {
            AlwaysOnTopChanged?.Invoke(this, alwaysOnTop);
        }
    }

    private void RestorePosition()
    {
        if (_window is null)
        {
            return;
        }

        double left = ParseDouble(_settings.Get(FloatingWidgetSettings.LeftKey, string.Empty));
        double top = ParseDouble(_settings.Get(FloatingWidgetSettings.TopKey, string.Empty));

        var windowSize = _window.AppWindow.Size;
        var (clampedLeft, clampedTop) = WidgetPositionHelper.Clamp(
            left,
            top,
            windowSize.Width,
            windowSize.Height);

        _window.MoveTo((int)clampedLeft, (int)clampedTop);
    }

    private void SavePosition()
    {
        if (_window is null)
        {
            return;
        }

        var pos = _window.GetPosition();
        _settings.Set(FloatingWidgetSettings.LeftKey, pos.X.ToString("F0", CultureInfo.InvariantCulture));
        _settings.Set(FloatingWidgetSettings.TopKey, pos.Y.ToString("F0", CultureInfo.InvariantCulture));
        _settings.Save();
    }

    private bool GetAlwaysOnTop()
    {
        var value = _settings.Get(FloatingWidgetSettings.AlwaysOnTopKey, "true");
        return !bool.TryParse(value, out var topmost) || topmost;
    }

    private static double ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private void RunOnUi(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcherQueue.TryEnqueue(() => action());
    }
}

public static class FloatingWidgetSettings
{
    public const string EnabledKey = "FloatingWidgetEnabled";
    public const string AlwaysOnTopKey = "FloatingWidgetAlwaysOnTop";
    public const string LeftKey = "FloatingWidgetLeft";
    public const string TopKey = "FloatingWidgetTop";
}

public static class WidgetPositionHelper
{
    public static (double Left, double Top) Clamp(double left, double top, double width, double height) =>
        Clamp(left, top, width, height, GetWorkAreas());

    /// <summary>
    /// Keeps a window of the given size entirely inside one monitor's work area.
    /// </summary>
    /// <remarks>
    /// The monitor is the one the window currently overlaps most, so dragging across
    /// monitors keeps working and the window is constrained by the work area of
    /// whichever monitor it is on. When the position overlaps nothing at all - a
    /// saved position on a monitor that has since been disconnected, a resolution
    /// change, a moved taskbar - the nearest work area is used so the window is
    /// always recovered into view instead of being pinned to an arbitrary monitor.
    /// <para>
    /// All values are physical pixels, the same space <c>AppWindow.Position</c> and
    /// <c>AppWindow.Size</c> use, so the actual scaled window size is honoured and
    /// DIPs are never mixed with pixels.
    /// </para>
    /// </remarks>
    public static (double Left, double Top) Clamp(
        double left,
        double top,
        double width,
        double height,
        IReadOnlyList<Rect> workAreas)
    {
        if (workAreas.Count == 0)
        {
            return (left, top);
        }

        var area = SelectWorkArea(left, top, width, height, workAreas);

        // Math.Max keeps the maximum at the area origin when the window is larger
        // than the work area, so the window is never pushed past the edge.
        var maxLeft = Math.Max(area.X, area.X + area.Width - width);
        var maxTop = Math.Max(area.Y, area.Y + area.Height - height);
        return (Math.Clamp(left, area.X, maxLeft), Math.Clamp(top, area.Y, maxTop));
    }

    /// <summary>Picks the work area a window at this position belongs in.</summary>
    public static Rect SelectWorkArea(
        double left,
        double top,
        double width,
        double height,
        IReadOnlyList<Rect> workAreas)
    {
        if (workAreas.Count == 0)
        {
            return default;
        }

        var best = workAreas[0];
        var bestOverlap = 0.0;

        foreach (var area in workAreas)
        {
            var overlapX = Math.Max(0, Math.Min(left + width, area.X + area.Width) - Math.Max(left, area.X));
            var overlapY = Math.Max(0, Math.Min(top + height, area.Y + area.Height) - Math.Max(top, area.Y));
            var overlap = overlapX * overlapY;
            if (overlap > bestOverlap)
            {
                bestOverlap = overlap;
                best = area;
            }
        }

        if (bestOverlap > 0)
        {
            return best;
        }

        // The window overlaps no work area at all - a saved position on a monitor
        // that has since been disconnected, a resolution change, a moved taskbar -
        // so recover into the nearest one. A tie-break on centre distance keeps that
        // deterministic instead of pinning the window to an arbitrary monitor.
        var bestDistance = double.MaxValue;
        foreach (var area in workAreas)
        {
            var distance = DistanceToCentre(left, top, width, height, area);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = area;
            }
        }

        return best;
    }

    private static double DistanceToCentre(double left, double top, double width, double height, Rect area)
    {
        var dx = (left + (width / 2)) - (area.X + (area.Width / 2));
        var dy = (top + (height / 2)) - (area.Y + (area.Height / 2));
        return (dx * dx) + (dy * dy);
    }

    public static IReadOnlyList<Rect> GetWorkAreas()
    {
        // CsWinRT's IReadOnlyListImpl enumerator throws InvalidCastException for the
        // DisplayArea.FindAll projection, which aborted Show() before the window was
        // ever displayed. Index the list instead of foreach-ing it.
        var displays = DisplayArea.FindAll();
        var result = new List<Rect>(displays.Count);
        for (var i = 0; i < displays.Count; i++)
        {
            var work = displays[i].WorkArea;
            result.Add(new Rect(work.X, work.Y, work.Width, work.Height));
        }

        return result;
    }
}
