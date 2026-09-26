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
    }

    public event EventHandler? IsVisibleChanged;

    public event EventHandler<bool>? AlwaysOnTopChanged;

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

    public void Show()
    {
        RunOnUi(() =>
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
            SaveEnabled(true);
        });
    }

    public void Hide()
    {
        RunOnUi(() =>
        {
            if (_window is not null)
            {
                SavePosition();
                _window.HideWidget();
                _viewModel?.Suspend();
            }

            IsVisible = false;
            SaveEnabled(false);
        });
    }

    public void Toggle()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    public void RestoreIfEnabled()
    {
        if (GetEnabled())
        {
            Show();
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
        RunOnUi(() =>
        {
            if (_window is not null)
            {
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
    }

    private ViewModels.FloatingWidgetViewModel CreateWidgetViewModel() =>
        new(
            _services.GetRequiredService<INetworkTrafficCollector>(),
            _services.GetRequiredService<INetworkAdapterProvider>(),
            _localization,
            _dispatcherQueue);

    private void OnCloseRequested(object? sender, EventArgs e) => Hide();

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

        var (clampedLeft, clampedTop) = WidgetPositionHelper.Clamp(
            left,
            top,
            Views.FloatingWidgetWindow.WidgetWidth,
            Views.FloatingWidgetWindow.WidgetHeight);

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

    private bool GetEnabled()
    {
        var value = _settings.Get(FloatingWidgetSettings.EnabledKey, "false");
        return bool.TryParse(value, out var enabled) && enabled;
    }

    private bool GetAlwaysOnTop()
    {
        var value = _settings.Get(FloatingWidgetSettings.AlwaysOnTopKey, "true");
        return !bool.TryParse(value, out var topmost) || topmost;
    }

    private void SaveEnabled(bool enabled)
    {
        _settings.Set(FloatingWidgetSettings.EnabledKey, enabled.ToString());
        _settings.Save();
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
    public static (double Left, double Top) Clamp(double left, double top, double width, double height)
    {
        var areas = GetWorkAreas();
        if (areas.Count == 0)
        {
            return (left, top);
        }

        var best = areas[0];
        var bestOverlap = 0.0;

        foreach (var area in areas)
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

        var clampedLeft = Math.Clamp(left, best.X, Math.Max(best.X, best.X + best.Width - width));
        var clampedTop = Math.Clamp(top, best.Y, Math.Max(best.Y, best.Y + best.Height - height));
        return (clampedLeft, clampedTop);
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
