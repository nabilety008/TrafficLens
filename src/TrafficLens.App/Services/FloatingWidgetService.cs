using System.Globalization;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TrafficLens.App.ViewModels;
using TrafficLens.App.Views;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;

namespace TrafficLens.App.Services;

/// <summary>
/// Owns the single floating widget instance. Show/Hide are idempotent (repeat
/// Show only activates the existing window), position and always-on-top state
/// are persisted through the existing <see cref="ISettingsService"/>, and the
/// widget window never keeps the app alive: closing the widget only hides it,
/// while closing the main window (and app exit) disposes it via
/// <see cref="Dispose"/>.
/// </summary>
public sealed class FloatingWidgetService : IFloatingWidgetService
{
    private readonly ISettingsService _settings;
    private readonly IServiceProvider _services;
    private readonly ILocalizationService _localization;

    private FloatingWidgetWindow? _window;
    private FloatingWidgetViewModel? _viewModel;
    private bool _isVisible;

    public FloatingWidgetService(
        ISettingsService settings,
        IServiceProvider services,
        ILocalizationService localization)
    {
        _settings = settings;
        _services = services;
        _localization = localization;
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
        EnsureCreated();
        if (_window is null)
        {
            return;
        }

        RestorePosition();
        ApplyAlwaysOnTop(GetAlwaysOnTop(), notify: false);
        _window.Show();
        _window.Activate();
        IsVisible = true;
        SaveEnabled(true);
    }

public void Hide()
        {
            if (_window is null)
            {
                return;
            }

            SavePosition();
            _window.Hide();
            IsVisible = false;
            SaveEnabled(false);

            // Suspend (not dispose) the view model to stop background event
            // processing while hidden. The window remains alive so Show() can
            // re-activate it with a simple Resume().
            _viewModel?.Suspend();
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

    private void ApplyAlwaysOnTop(bool alwaysOnTop, bool notify)
    {
        if (_window is not null)
        {
            _window.Topmost = alwaysOnTop;
            _viewModel!.IsPinned = alwaysOnTop;
        }

        _settings.Set(FloatingWidgetSettings.AlwaysOnTopKey, alwaysOnTop.ToString());
        _settings.Save();

        if (notify)
        {
            AlwaysOnTopChanged?.Invoke(this, alwaysOnTop);
        }
    }

    public void Dispose()
    {
        if (_window is not null)
        {
            SavePosition();
            _window.Closing -= OnWindowClosing;
            _window.Close();
            _window = null;
        }

        if (_viewModel is not null)
        {
            DisposeWidgetViewModel(_viewModel);
            _viewModel = null;
        }
    }

    private void EnsureCreated()
    {
        if (_window is not null)
        {
            // Window already exists — resume if previously suspended
            _viewModel?.Resume();
            return;
        }

        _viewModel = CreateWidgetViewModel();
        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.PinStateChanged += OnPinStateChanged;

        _window = new FloatingWidgetWindow(_viewModel);
        _window.Closing += OnWindowClosing;
    }

    private FloatingWidgetViewModel CreateWidgetViewModel() =>
        new(
            _services.GetRequiredService<INetworkTrafficCollector>(),
            _services.GetRequiredService<INetworkAdapterProvider>(),
            _localization);

    private void DisposeWidgetViewModel(FloatingWidgetViewModel viewModel)
    {
        viewModel.CloseRequested -= OnCloseRequested;
        viewModel.PinStateChanged -= OnPinStateChanged;
        viewModel.Dispose();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Hide();

    private void OnPinStateChanged(object? sender, bool isPinned) =>
        ApplyAlwaysOnTop(isPinned, notify: true);

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
            _window.Width,
            _window.Height,
            GetWorkAreas());

        _window.Left = clampedLeft;
        _window.Top = clampedTop;
    }

    private void SavePosition()
    {
        if (_window is null)
        {
            return;
        }

        _settings.Set(FloatingWidgetSettings.LeftKey, _window.Left.ToString("F0", CultureInfo.InvariantCulture));
        _settings.Set(FloatingWidgetSettings.TopKey, _window.Top.ToString("F0", CultureInfo.InvariantCulture));
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

    private static IReadOnlyList<Rect> GetWorkAreas() =>
        new[]
        {
            new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight)
        };
}

/// <summary>
/// Settings keys used by <see cref="FloatingWidgetService"/>. Kept together so
/// tests and call sites cannot drift from the actual keys.
/// </summary>
public static class FloatingWidgetSettings
{
    public const string EnabledKey = "FloatingWidgetEnabled";
    public const string AlwaysOnTopKey = "FloatingWidgetAlwaysOnTop";
    public const string LeftKey = "FloatingWidgetLeft";
    public const string TopKey = "FloatingWidgetTop";
}