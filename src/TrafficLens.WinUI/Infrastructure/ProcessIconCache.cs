using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace TrafficLens.WinUI.Infrastructure;

public sealed class ProcessIconCache
{
    public const int CacheCapacity = 256;
    public const int MaxExtractionsPerRefresh = 8;

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ConcurrentDictionary<string, BitmapImage> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _requested = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _insertionOrder = new();
    private readonly object _evictSync = new();
    private BitmapImage? _fallback;
    private byte[]? _fallbackPng;
    private bool _fallbackLoading;
    private int _extractionsThisRefresh;
    private bool _disposed;

    public ProcessIconCache(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        Log("cache-created");
    }

    public event EventHandler<string>? IconReady;

    public BitmapImage? Fallback => _fallback;

    public void BeginRefresh()
    {
        _extractionsThisRefresh = 0;
    }

    public BitmapImage? TryGet(string? executablePath, bool iconAvailable)
    {
        if (_disposed)
        {
            return _fallback;
        }

        if (!iconAvailable || string.IsNullOrWhiteSpace(executablePath))
        {
            return _fallback;
        }

        if (_images.TryGetValue(executablePath, out var image))
        {
            return image;
        }

        if (_requested.ContainsKey(executablePath))
        {
            return _fallback;
        }

        return null;
    }

    public void Request(string? executablePath, bool iconAvailable)
    {
        if (_disposed)
        {
            return;
        }

        if (!iconAvailable || string.IsNullOrWhiteSpace(executablePath))
        {
            _ = EnsureFallbackAsync();
            return;
        }

        if (_images.ContainsKey(executablePath) || _requested.ContainsKey(executablePath))
        {
            _ = EnsureFallbackAsync();
            return;
        }

        if (_extractionsThisRefresh >= MaxExtractionsPerRefresh)
        {
            return;
        }

        if (!_requested.TryAdd(executablePath, 0))
        {
            return;
        }

        _extractionsThisRefresh++;
        _ = ExtractAsync(executablePath);
    }

    public void Dispose()
    {
        _disposed = true;
        IconReady = null;
    }

    private async Task ExtractAsync(string path)
    {
        try
        {
            await EnsureFallbackAsync().ConfigureAwait(false);

            var png = await Task.Run(() => ExtractPngSafe(path)).ConfigureAwait(false);
            if (_disposed)
            {
                return;
            }

            if (png is null)
            {
                png = await ExtractOnUiAsync(path).ConfigureAwait(false);
            }

            if (_disposed)
            {
                return;
            }

            if (png is null)
            {
                Log($"extract-fail path={path}");
                CacheFallbackForPath(path);
                return;
            }

            Log($"extract-ok path={path} bytes={png.Length}");

            await _dispatcherQueue.TryEnqueueAsync(async () =>
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    var image = await DecodePngAsync(png).ConfigureAwait(true);
                    CacheImage(path, image, notify: true);
                }
                catch (Exception ex)
                {
                    Log($"decode-fail path={path} err={ex.Message}");
                    CacheFallbackForPath(path);
                }
            });
        }
        catch (Exception ex)
        {
            Log($"extract-err path={path} err={ex.Message}");
            if (!_disposed)
            {
                CacheFallbackForPath(path);
            }
        }
    }

    private Task<byte[]?> ExtractOnUiAsync(string path)
    {
        var completion = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var ok = _dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                completion.TrySetResult(ExtractPngSafe(path));
            }
            catch (Exception ex)
            {
                Log($"ui-extract-err path={path} err={ex.Message}");
                completion.TrySetResult(null);
            }
        });

        if (!ok)
        {
            completion.TrySetResult(null);
        }

        return completion.Task;
    }

    public static void Log(string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TrafficLens", "logs");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"icons-{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(file, $"{DateTime.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private void CacheFallbackForPath(string path)
    {
        if (_disposed)
        {
            return;
        }

        _ = _dispatcherQueue.TryEnqueueAsync(async () =>
        {
            if (_disposed)
            {
                return;
            }

            await EnsureFallbackAsync().ConfigureAwait(true);
            if (_fallback is not null)
            {
                CacheImage(path, _fallback, notify: true);
            }
        });
    }

    private void CacheImage(string path, BitmapImage image, bool notify)
    {
        if (_disposed)
        {
            return;
        }

        if (!_images.TryAdd(path, image))
        {
            return;
        }

        EvictIfNeeded(path);

        if (notify)
        {
            IconReady?.Invoke(this, path);
        }
    }

    private void EvictIfNeeded(string path)
    {
        lock (_evictSync)
        {
            _insertionOrder.Enqueue(path);

            while (_images.Count > CacheCapacity && _insertionOrder.Count > 0)
            {
                var oldest = _insertionOrder.Dequeue();
                if (string.Equals(oldest, path, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (_images.TryRemove(oldest, out _))
                {
                    _requested.TryRemove(oldest, out _);
                }
            }
        }
    }

    private async Task EnsureFallbackAsync()
    {
        if (_disposed || _fallback is not null)
        {
            return;
        }

        if (!_fallbackLoading)
        {
            _fallbackLoading = true;
            _fallbackPng ??= CreateFallbackPng();
        }

        await _dispatcherQueue.TryEnqueueAsync(async () =>
        {
            if (_disposed || _fallback is not null || _fallbackPng is null)
            {
                _fallbackLoading = false;
                return;
            }

            try
            {
                _fallback = await DecodePngAsync(_fallbackPng).ConfigureAwait(true);
                IconReady?.Invoke(this, string.Empty);
            }
            catch
            {
                _fallback = new BitmapImage();
            }
            finally
            {
                _fallbackLoading = false;
            }
        });
    }

    private static byte[] CreateFallbackPng()
    {
        using var bitmap = new System.Drawing.Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.Clear(System.Drawing.Color.Transparent);
            using var body = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0x33, 0x33, 0x4A));
            using var border = new System.Drawing.Pen(System.Drawing.Color.FromArgb(0x9A, 0x9A, 0xB0), 2);
            g.FillRectangle(body, 4, 6, 24, 20);
            g.DrawRectangle(border, 4, 6, 24, 20);
            using var bar = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0x9A, 0x9A, 0xB0));
            g.FillRectangle(bar, 9, 11, 14, 3);
            g.FillRectangle(bar, 9, 17, 10, 3);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return stream.ToArray();
    }

    private static byte[]? ExtractPngSafe(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Log($"missing path={path}");
                return null;
            }

            try
            {
                using var associated = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (associated is not null)
                {
                    using var associatedBitmap = associated.ToBitmap();
                    using var associatedStream = new MemoryStream();
                    associatedBitmap.Save(associatedStream, System.Drawing.Imaging.ImageFormat.Png);
                    if (associatedStream.Length > 0)
                    {
                        return associatedStream.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"associated-err path={path} err={ex.Message}");
            }

            var info = new ShFileInfo();
            var size = (uint)Marshal.SizeOf<ShFileInfo>();
            var handle = ShGetFileInfo(path, FileAttributeNormal, ref info, size, ShgfiIcon | ShgfiLargeIcon);
            if (handle == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            {
                Log($"shfail path={path} handle={handle} icon={info.hIcon} size={size}");
                return null;
            }

            try
            {
                using var bitmap = System.Drawing.Bitmap.FromHicon(info.hIcon);
                using var stream = new MemoryStream();
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        catch (Exception ex)
        {
            Log($"safe-err path={path} err={ex.Message}");
            return null;
        }
    }

    private static async Task<BitmapImage> DecodePngAsync(byte[] png)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(png);
            await writer.StoreAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        var image = new BitmapImage();
        await image.SetSourceAsync(stream);
        return image;
    }

    private const uint ShgfiIcon = 0x00000100;
    private const uint ShgfiLargeIcon = 0x0;
    private const uint FileAttributeNormal = 0x80;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetFileInfo")]
    private static extern IntPtr ShGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref ShFileInfo psfi,
        uint cbSizeFileInfo,
        uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}

internal static class DispatcherQueueAsyncExtensions
{
    public static Task<bool> TryEnqueueAsync(this DispatcherQueue queue, Func<Task> work)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var ok = queue.TryEnqueue(async () =>
        {
            try
            {
                await work().ConfigureAwait(true);
                completion.TrySetResult(true);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        if (!ok)
        {
            completion.TrySetResult(false);
        }

        return completion.Task;
    }
}
