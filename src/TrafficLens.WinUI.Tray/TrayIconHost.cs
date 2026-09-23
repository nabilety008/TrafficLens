using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrafficLens.WinUI.Tray;

public sealed class TrayMenuLabels
{
    public string OpenText { get; init; } = string.Empty;
    public string ToggleWidgetText { get; init; } = string.Empty;
    public string AlwaysOnTopText { get; init; } = string.Empty;
    public string ExitText { get; init; } = string.Empty;
    public bool AlwaysOnTopChecked { get; init; }
    public bool RightToLeft { get; init; }
}

public sealed class TrayIconHost : IDisposable
{
    private NotifyIcon? _icon;
    private ContextMenuStrip? _menu;
    private ToolStripMenuItem? _openItem;
    private ToolStripMenuItem? _toggleWidgetItem;
    private ToolStripMenuItem? _alwaysOnTopItem;
    private ToolStripMenuItem? _exitItem;
    private Icon? _brandIcon;
    private bool _ownsIconHandle;
    private IntPtr _iconHandle;
    private bool _disposed;

    public event EventHandler? OpenRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler? ToggleWidgetRequested;

    public event EventHandler? ToggleAlwaysOnTopRequested;

    public event EventHandler? BalloonClicked;

    public void Show()
    {
        if (_disposed)
        {
            return;
        }

        EnsureCreated();
        if (_icon is not null)
        {
            _icon.Visible = true;
        }
    }

    public void Hide()
    {
        if (_icon is not null)
        {
            _icon.Visible = false;
        }
    }

    public void UpdateMenu(TrayMenuLabels labels)
    {
        if (_disposed)
        {
            return;
        }

        EnsureCreated();

        if (_menu is not null)
        {
            _menu.RightToLeft = labels.RightToLeft
                ? System.Windows.Forms.RightToLeft.Yes
                : System.Windows.Forms.RightToLeft.No;
        }

        if (_openItem is not null)
        {
            _openItem.Text = labels.OpenText;
        }

        if (_toggleWidgetItem is not null)
        {
            _toggleWidgetItem.Text = labels.ToggleWidgetText;
        }

        if (_alwaysOnTopItem is not null)
        {
            _alwaysOnTopItem.Text = labels.AlwaysOnTopText;
            _alwaysOnTopItem.Checked = labels.AlwaysOnTopChecked;
        }

        if (_exitItem is not null)
        {
            _exitItem.Text = labels.ExitText;
        }
    }

    public void ShowBalloon(string title, string text, bool warning)
    {
        if (_disposed)
        {
            return;
        }

        EnsureCreated();
        if (_icon is null)
        {
            return;
        }

        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = text;
        _icon.BalloonTipIcon = warning ? ToolTipIcon.Warning : ToolTipIcon.Info;
        _icon.ShowBalloonTip(warning ? 8000 : 4000);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_icon is not null)
        {
            _icon.Visible = false;
            _icon.ContextMenuStrip = null;
            _icon.DoubleClick -= OnOpen;
            _icon.BalloonTipClicked -= OnBalloonClicked;
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
            if (_ownsIconHandle)
            {
                NativeMethods.DestroyIcon(_iconHandle);
            }

            _iconHandle = IntPtr.Zero;
        }

        if (_brandIcon is not null)
        {
            _brandIcon.Dispose();
            _brandIcon = null;
        }
    }

    private void EnsureCreated()
    {
        if (_icon is not null)
        {
            return;
        }

        _brandIcon = TryLoadBrandIcon();
        if (_brandIcon is not null)
        {
            _iconHandle = _brandIcon.Handle;
            _ownsIconHandle = false;
        }
        else
        {
            _iconHandle = CreateTrayIconHandle();
            _ownsIconHandle = true;
        }

        _openItem = new ToolStripMenuItem(string.Empty);
        _openItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        _toggleWidgetItem = new ToolStripMenuItem(string.Empty);
        _toggleWidgetItem.Click += (_, _) => ToggleWidgetRequested?.Invoke(this, EventArgs.Empty);

        _alwaysOnTopItem = new ToolStripMenuItem(string.Empty);
        _alwaysOnTopItem.Click += (_, _) => ToggleAlwaysOnTopRequested?.Invoke(this, EventArgs.Empty);

        _exitItem = new ToolStripMenuItem(string.Empty);
        _exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _menu = new ContextMenuStrip
        {
            ShowImageMargin = false
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
        _icon.DoubleClick += OnOpen;
        _icon.BalloonTipClicked += OnBalloonClicked;
    }

    private void OnOpen(object? sender, EventArgs e) => OpenRequested?.Invoke(this, EventArgs.Empty);

    private void OnBalloonClicked(object? sender, EventArgs e) => BalloonClicked?.Invoke(this, EventArgs.Empty);

    private static Icon? TryLoadBrandIcon()
    {
        try
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Assets", "TrafficLens.ico"),
                Path.Combine(AppContext.BaseDirectory, "TrafficLens.ico"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "branding", "TrafficLens.ico")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    using var stream = File.OpenRead(path);
                    return new Icon(stream, 32, 32);
                }
            }
        }
        catch (Exception)
        {
        }

        return null;
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

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool DestroyIcon(IntPtr handle);
    }
}
