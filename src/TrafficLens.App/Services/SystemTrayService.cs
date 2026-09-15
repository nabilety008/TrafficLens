using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using TrafficLens.Core.Abstractions;
using TrafficLens.Core.Localization;

namespace TrafficLens.App.Services;

/// <summary>
/// Owns the single system tray icon (TL-011). The NotifyIcon and its context
/// menu are created once on the WPF Dispatcher thread, the icon is drawn at
/// runtime (no external asset), culture changes relabel the existing menu in
/// place (never recreating the icon), and <see cref="Dispose"/> removes the
/// icon and frees the native handle so no ghost icon can remain.
/// </summary>
public sealed class SystemTrayService : ISystemTrayService
{
    private readonly ILocalizationService _localization;
    private readonly IFloatingWidgetService _floatingWidgetService;
    private readonly ISettingsService _settings;
    private readonly ILogger<SystemTrayService> _logger;

    private NotifyIcon? _icon;
    private ContextMenuStrip? _menu;
    private ToolStripMenuItem? _openItem;
    private ToolStripMenuItem? _toggleWidgetItem;
    private ToolStripMenuItem? _alwaysOnTopItem;
    private ToolStripMenuItem? _exitItem;
    private IntPtr _iconHandle;
    private bool _disposed;

    public SystemTrayService(
        ILocalizationService localization,
        IFloatingWidgetService floatingWidgetService,
        ISettingsService settings,
        ILogger<SystemTrayService> logger)
    {
        _localization = localization;
        _floatingWidgetService = floatingWidgetService;
        _settings = settings;
        _logger = logger;
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? ExitRequested;

    public void Show()
    {
        RunOnUi(() =>
        {
            EnsureCreated();
            if (_icon is not null)
            {
                _icon.Visible = true;
            }
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
            if (_icon is null)
            {
                return;
            }

            _icon.BalloonTipTitle = "TrafficLens";
            _icon.BalloonTipText = _localization["TrayCloseNoticeBalloon"];
            _icon.BalloonTipIcon = ToolTipIcon.Info;
            _icon.ShowBalloonTip(4000);

            TrayBehavior.MarkCloseNoticeShown(_settings);
            _settings.Save();
        });
    }

    public void ShowAlert(string title, string message)
    {
        RunOnUi(() =>
        {
            if (_disposed || _icon is null)
            {
                _logger.LogWarning("Alert notification dropped: system tray is unavailable");
                return;
            }

            EnsureCreated();
            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = message;
            _icon.BalloonTipIcon = ToolTipIcon.Warning;
            _icon.ShowBalloonTip(8000);
        });
    }

    public void Dispose()
    {
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

                if (_icon is not null)
                {
                    _icon.Visible = false;
                    _icon.ContextMenuStrip = null;
                    _icon.Dispose();
                    _icon = null;
                }

                if (_menu is not null)
                {
                    _menu.Dispose();
                    _menu = null;
                }

                if (_iconHandle != IntPtr.Zero)
                {
                    NativeMethods.DestroyIcon(_iconHandle);
                    _iconHandle = IntPtr.Zero;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispose system tray icon during shutdown");
        }
    }

    private void EnsureCreated()
    {
        if (_icon is not null)
        {
            return;
        }

        _iconHandle = CreateTrayIconHandle();

        _openItem = new ToolStripMenuItem(GetString("OpenTrafficLensLabel"));
        _openItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        _toggleWidgetItem = new ToolStripMenuItem(GetToggleWidgetLabel());
        _toggleWidgetItem.Click += (_, _) => _floatingWidgetService.Toggle();

        _alwaysOnTopItem = new ToolStripMenuItem(GetString("AlwaysOnTopLabel"))
        {
            Checked = _floatingWidgetService.IsAlwaysOnTop
        };
        _alwaysOnTopItem.Click += (_, _) =>
        {
            _alwaysOnTopItem.Checked = !_alwaysOnTopItem.Checked;
            _floatingWidgetService.ToggleAlwaysOnTop();
        };

        _exitItem = new ToolStripMenuItem(GetString("ExitLabel"));
        _exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            RightToLeft = _localization.IsRightToLeft
                ? System.Windows.Forms.RightToLeft.Yes
                : System.Windows.Forms.RightToLeft.No
        };
        _menu.Items.Add(_openItem);
        _menu.Items.Add(_toggleWidgetItem);
        _menu.Items.Add(_alwaysOnTopItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_exitItem);

        _icon = new NotifyIcon
        {
            Icon = System.Drawing.Icon.FromHandle(_iconHandle),
            Text = "TrafficLens",
            ContextMenuStrip = _menu,
            Visible = false
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _icon.BalloonTipClicked += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        _floatingWidgetService.IsVisibleChanged += OnWidgetVisibilityChanged;
        _floatingWidgetService.AlwaysOnTopChanged += OnAlwaysOnTopChanged;
        _localization.CultureChanged += OnCultureChanged;
    }

    private static IntPtr CreateTrayIconHandle()
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            using var backgroundPath = RoundedRectPath(new Rectangle(2, 2, size - 4, size - 4), 9);
            using var backgroundBrush = new SolidBrush(Color.FromArgb(0xFF, 0x1E, 0x1E, 0x2E));
            graphics.FillPath(backgroundBrush, backgroundPath);

            using var downBrush = new SolidBrush(Color.FromArgb(0xFF, 0x4F, 0xC3, 0xF7));
            using var upBrush = new SolidBrush(Color.FromArgb(0xFF, 0x26, 0xA6, 0x9A));

            graphics.FillPolygon(downBrush, new[]
            {
                new PointF(9, 11),
                new PointF(14, 17),
                new PointF(19, 11)
            });

            graphics.FillPolygon(upBrush, new[]
            {
                new PointF(9, 22),
                new PointF(14, 16),
                new PointF(19, 22)
            });
        }

        return bitmap.GetHicon();
    }

    private static GraphicsPath RoundedRectPath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void OnWidgetVisibilityChanged(object? sender, EventArgs e) =>
        RunOnUi(() =>
        {
            if (_toggleWidgetItem is not null)
            {
                _toggleWidgetItem.Text = GetToggleWidgetLabel();
            }
        });

    private void OnAlwaysOnTopChanged(object? sender, bool isAlwaysOnTop) =>
        RunOnUi(() =>
        {
            if (_alwaysOnTopItem is not null)
            {
                _alwaysOnTopItem.Checked = isAlwaysOnTop;
            }
        });

    private void OnCultureChanged(object? sender, EventArgs e) =>
        RunOnUi(RefreshLabels);

    private void RefreshLabels()
    {
        if (_menu is not null)
        {
            _menu.RightToLeft = _localization.IsRightToLeft
                ? System.Windows.Forms.RightToLeft.Yes
                : System.Windows.Forms.RightToLeft.No;
        }

        if (_openItem is not null)
        {
            _openItem.Text = GetString("OpenTrafficLensLabel");
        }

        if (_toggleWidgetItem is not null)
        {
            _toggleWidgetItem.Text = GetToggleWidgetLabel();
        }

        if (_alwaysOnTopItem is not null)
        {
            _alwaysOnTopItem.Text = GetString("AlwaysOnTopLabel");
        }

        if (_exitItem is not null)
        {
            _exitItem.Text = GetString("ExitLabel");
        }
    }

    private string GetToggleWidgetLabel() =>
        _floatingWidgetService.IsVisible
            ? GetString("HideFloatingWidgetLabel")
            : GetString("ShowFloatingWidgetLabel");

    private string GetString(string key) => _localization[key];

    private void RunOnUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool DestroyIcon(IntPtr handle);
    }
}