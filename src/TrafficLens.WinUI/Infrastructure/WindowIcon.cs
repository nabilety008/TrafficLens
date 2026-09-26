using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace TrafficLens.WinUI.Infrastructure;

internal static class WindowIcon
{
    private const uint WmSetIcon = 0x0080;
    private const int IconSmall = 0;
    private const int IconBig = 1;
    private const int IconSmall2 = 2;
    private const uint ImageIcon = 1;
    private const uint LoadFromFile = 0x00000010;
    private const int BaseBigSize = 32;
    private const int BaseSmallSize = 16;

    private static readonly object Gate = new();
    private static readonly Dictionary<int, IntPtr> Handles = new();

    public static void Apply(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "TrafficLens.ico");
        if (!File.Exists(path))
        {
            return;
        }

        var scale = Math.Max(1.0, GetDpiForWindow(hwnd) / 96.0);
        SendMessage(hwnd, WmSetIcon, IconBig, Load(path, (int)Math.Round(BaseBigSize * scale)));
        SendMessage(hwnd, WmSetIcon, IconSmall, Load(path, (int)Math.Round(BaseSmallSize * scale)));
        SendMessage(hwnd, WmSetIcon, IconSmall2, Load(path, (int)Math.Round(BaseSmallSize * scale)));
    }

    private static IntPtr Load(string path, int size)
    {
        lock (Gate)
        {
            if (Handles.TryGetValue(size, out var cached))
            {
                return cached;
            }

            var handle = LoadImage(IntPtr.Zero, path, ImageIcon, size, size, LoadFromFile);
            Handles[size] = handle;
            return handle;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInstance, string name, uint type, int cx, int cy, uint load);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);
}
