using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TrafficLens.App.Services;

/// <summary>
/// Bounded cache of process executable icons. Extraction happens only on the UI
/// thread (never on the ETW hot path) and at most <see cref="MaxExtractionsPerRefresh"/>
/// unique paths are extracted per refresh — the cache is keyed by executable
/// path so the same exe is never extracted again. Failed or missing paths map
/// to a cached generic fallback icon; protected/exited processes can never
/// crash the UI.
/// </summary>
public sealed class ProcessIconResolver
{
    public const int CacheCapacity = 256;
    public const int MaxExtractionsPerRefresh = 8;

    private readonly object _sync = new();
    private readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _insertionOrder = new();
    private ImageSource? _fallback;

    public ImageSource? GetOrExtract(string? executablePath, bool iconAvailable)
    {
        if (!iconAvailable || string.IsNullOrWhiteSpace(executablePath))
        {
            return FallbackIcon;
        }

        lock (_sync)
        {
            if (_cache.TryGetValue(executablePath, out var cached))
            {
                return cached;
            }
        }

        var icon = ExtractAssociatedIconSafe(executablePath) ?? FallbackIcon;
        lock (_sync)
        {
            if (!_cache.ContainsKey(executablePath))
            {
                _cache[executablePath] = icon;
                _insertionOrder.Enqueue(executablePath);

                while (_cache.Count > CacheCapacity && _insertionOrder.Count > 0)
                {
                    var oldest = _insertionOrder.Dequeue();
                    _cache.Remove(oldest);
                }
            }
        }

        return icon;
    }

    public ImageSource FallbackIcon
    {
        get
        {
            if (_fallback is null)
            {
                _fallback = CreateFallbackIcon();
            }

            return _fallback;
        }
    }

    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint FileAttributeNormal = 0x000000080;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static ImageSource? ExtractAssociatedIconSafe(string path)
    {
        try
        {
            var info = new SHFILEINFO();
            var size = (uint)Marshal.SizeOf<SHFILEINFO>();
            var handle = SHGetFileInfo(path, FileAttributeNormal, ref info, size, ShgfiIcon | ShgfiLargeIcon);
            if (handle == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    info.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource CreateFallbackIcon()
    {
        var drawing = new DrawingGroup();
        var geometry = new GeometryGroup();
        geometry.Children.Add(new RectangleGeometry(new Rect(1, 2, 14, 12), 2, 2));
        geometry.Children.Add(new RectangleGeometry(new Rect(4, 5, 8, 2), 1, 1));

        var pen = new Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9A, 0x9A, 0xB0)), 1);
        pen.Freeze();
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x4A));
        brush.Freeze();
        drawing.Children.Add(new GeometryDrawing(brush, pen, geometry));
        drawing.Freeze();

        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }
}