using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace TrafficLens.WinUI.Infrastructure;

internal static class WindowIcon
{
    // Shell identity for the process. Without an explicit AppUserModelID the shell
    // synthesises one for this unpackaged app, finds nothing registered for it and
    // falls back to a generic application icon on the taskbar, in Alt+Tab and in
    // the thumbnail preview. Keep this in sync with the AppId in
    // packaging/TrafficLens.iss so installed and portable runs group as one app.
    private const string AppUserModelId = "8F0E8A8F-7B1D-4A5E-9C2D-3E5F6A7B8C9D";

    private const uint WmSetIcon = 0x0080;
    private const int IconSmall = 0;
    private const int IconBig = 1;
    private const int IconSmall2 = 2;
    private const uint ImageIcon = 1;
    private const uint LoadFromFile = 0x00000010;
    private const int BaseBigSize = 32;
    private const int BaseSmallSize = 16;
    // GetClassLongPtr index constants (see the nIndex table in the
    // GetClassLongPtrW reference). GCLP_HICONSM is -34, not -15; -15 is not a
    // class index at all and SetClassLongPtr silently discards a write to it.
    private const int GclpHIcon = -14;
    private const int GclpHIconSm = -34;

    private static readonly object Gate = new();
    private static readonly Dictionary<int, IntPtr> Handles = new();
    private static bool _appUserModelIdSet;
    private static bool _classIconSet;

    /// <summary>
    /// Must run before the first top-level window is created, otherwise the shell
    /// has already resolved this process to a synthetic identity.
    /// </summary>
    public static void SetAppUserModelId()
    {
        lock (Gate)
        {
            if (_appUserModelIdSet)
            {
                return;
            }

            _appUserModelIdSet = true;
        }

        SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
    }

    public static void Apply(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "TrafficLens.ico");
        if (!File.Exists(path))
        {
            return;
        }

        var scale = Math.Max(1.0, GetDpiForWindow(hwnd) / 96.0);
        var big = Load(path, (int)Math.Round(BaseBigSize * scale));
        var small = Load(path, (int)Math.Round(BaseSmallSize * scale));

        // The Windows App SDK registers WinUIDesktopWin32WindowClass with NULL
        // hIcon/hIconSm. Per the documented class-icon behaviour, a class without
        // icons makes the system substitute the DEFAULT APPLICATION ICON for the
        // large and small class icons, and that default is what the taskbar, the
        // Alt+Tab switcher and explorer display. Every top-level window here is
        // that one class and every one of them wants the same brand icon.
        ApplyClassIcon(hwnd, big, small);

        SendMessage(hwnd, WmSetIcon, IconBig, big);
        SendMessage(hwnd, WmSetIcon, IconSmall, small);
        SendMessage(hwnd, WmSetIcon, IconSmall2, small);

        // WM_SETICON only drives the per-window icon used by the title bar. The
        // taskbar, Alt+Tab and the thumbnail preview are driven by the shell, which
        // reads the AppWindow icon. SetIcon covers title bar and taskbar;
        // SetTaskbarIcon is the dedicated taskbar entry point.
        window.AppWindow.SetIcon(path);
        window.AppWindow.SetTaskbarIcon(path);
    }

    private static void ApplyClassIcon(IntPtr hwnd, IntPtr big, IntPtr small)
    {
        lock (Gate)
        {
            if (_classIconSet)
            {
                return;
            }

            _classIconSet = true;
        }

        SetClassLongPtr(hwnd, GclpHIcon, big);
        SetClassLongPtr(hwnd, GclpHIconSm, small);
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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInstance, string name, uint type, int cx, int cy, uint load);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
    private static extern IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);
}
