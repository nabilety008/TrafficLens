using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;
using TrafficLens.WinUI.Tray;

namespace TrafficLens.WinUI.Services;

public sealed class SystemTrayService : ISystemTrayService
{
    private readonly ILocalizationService _localization;
    private readonly IFloatingWidgetService _floatingWidgetService;
    private readonly ISettingsService _settings;
    private readonly ILogger<SystemTrayService> _logger;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly TrayIconHost _host;
    private bool _disposed;

    public SystemTrayService(
        ILocalizationService localization,
        IFloatingWidgetService floatingWidgetService,
        ISettingsService settings,
        ILogger<SystemTrayService> logger,
        DispatcherQueue dispatcherQueue)
    {
        _localization = localization;
        _floatingWidgetService = floatingWidgetService;
        _settings = settings;
        _logger = logger;
        _dispatcherQueue = dispatcherQueue;

        _host = new TrayIconHost();
        _host.OpenRequested += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _host.ExitRequested += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        _host.BalloonClicked += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _host.ToggleWidgetRequested += (_, _) => RunOnUi(() => _floatingWidgetService.Toggle());
        _host.ToggleAlwaysOnTopRequested += (_, _) => RunOnUi(() => _floatingWidgetService.ToggleAlwaysOnTop());
        _floatingWidgetService.IsVisibleChanged += OnWidgetVisibilityChanged;
        _floatingWidgetService.AlwaysOnTopChanged += OnAlwaysOnTopChanged;
        _localization.CultureChanged += OnCultureChanged;
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? ExitRequested;

    public void Show()
    {
        RunOnUi(() =>
        {
            if (_disposed)
            {
                return;
            }

            _host.Show();
            RefreshLabels();
        });
    }

    public void ShowFirstCloseToTrayNotice()
    {
        if (!TrayBehavior.ShouldShowFirstCloseNotice(_settings))
        {
            return;
        }

        RunOnUi(() =>
        {
            if (_disposed)
            {
                return;
            }

            _host.ShowBalloon(
                "TrafficLens",
                _localization["TrayCloseNoticeBalloon"],
                warning: false);

            TrayBehavior.MarkCloseNoticeShown(_settings);
            _settings.Save();
        });
    }

    public void ShowAlert(string title, string message)
    {
        RunOnUi(() =>
        {
            if (_disposed)
            {
                _logger.LogWarning("Alert notification dropped: system tray is unavailable");
                return;
            }

            _host.ShowBalloon(title, message, warning: true);
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            RunOnUi(() =>
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _localization.CultureChanged -= OnCultureChanged;
                _floatingWidgetService.IsVisibleChanged -= OnWidgetVisibilityChanged;
                _floatingWidgetService.AlwaysOnTopChanged -= OnAlwaysOnTopChanged;
                _host.Dispose();
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispose system tray icon during shutdown");
        }
    }

    private void OnWidgetVisibilityChanged(object? sender, EventArgs e) =>
        RunOnUi(RefreshLabels);

    private void OnAlwaysOnTopChanged(object? sender, bool isAlwaysOnTop) =>
        RunOnUi(RefreshLabels);

    private void OnCultureChanged(object? sender, EventArgs e) =>
        RunOnUi(RefreshLabels);

    private void RefreshLabels()
    {
        if (_disposed)
        {
            return;
        }

        _host.UpdateMenu(new TrayMenuLabels
        {
            OpenText = _localization["OpenTrafficLensLabel"],
            ToggleWidgetText = _floatingWidgetService.IsVisible
                ? _localization["HideFloatingWidgetLabel"]
                : _localization["ShowFloatingWidgetLabel"],
            AlwaysOnTopText = _localization["AlwaysOnTopLabel"],
            AlwaysOnTopChecked = _floatingWidgetService.IsAlwaysOnTop,
            ExitText = _localization["ExitLabel"],
            RightToLeft = _localization.IsRightToLeft
        });
    }

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
