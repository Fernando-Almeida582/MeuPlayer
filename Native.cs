using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace MeuPlayer;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public uint Size; public Rect Bounds, Work; public uint Flags; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X, Y; }

    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(long point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, nint name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string className, nint name);

    internal static (int X, int Y)? WindowPosition(nint window) =>
        GetWindowRect(window, out var rect) ? (rect.Left, rect.Top) : null;
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out uint value, int size);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint window, StringBuilder name, int capacity);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(nint window, int index, int style);

    internal static (Rect Bounds, Rect Work)? MainScreen()
    {
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(MonitorFromPoint(0, 1), ref info) ? (info.Bounds, info.Work) : null;
    }

    internal static bool FullScreenOn(nint ownWindow, Rect screen) =>
        IsFullScreenWindow(GetForegroundWindow(), ownWindow, screen);

    private static bool IsFullScreenWindow(nint foreground, nint ownWindow, Rect screen)
    {
        if (foreground == 0 || foreground == ownWindow || foreground == GetShellWindow() ||
            !GetWindowRect(foreground, out var rect)) return false;
        return rect.Left == screen.Left && rect.Top == screen.Top &&
            rect.Right == screen.Right && rect.Bottom == screen.Bottom && !IsShellSurface(foreground);
    }

    private static bool IsShellSurface(nint window)
    {
        var name = new StringBuilder(128);
        GetClassName(window, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or
            "DV2ControlHost" or "ImmersiveLauncher") return true;

        return IsStartSurface(window);
    }

    private static bool IsStartSurface(nint window)
    {
        if (window == 0) return false;

        GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0) return false;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return IsShellProcess(process.ProcessName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static bool IsShellProcess(string processName) => processName.ToUpperInvariant() is
        "STARTMENUEXPERIENCEHOST" or "SHELLEXPERIENCEHOST" or "SHELLHOST" or "SEARCHHOST" or "SEARCHAPP" or "SEARCHUI";

    private static bool LeftAligned()
    {
        try
        {
            object? alignment = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAl", null);
            if (alignment is int value) return value == 0;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException) { }
        return Environment.OSVersion.Version.Build < 22000;
    }

    internal static (int X, int Y) Dock(Rect screen, Rect work, int width, int height, double scale)
    {
        int margin = (int)(8 * scale);
        if (work.Bottom < screen.Bottom)
        {
            int x = screen.Left + margin;
            if (LeftAligned())
            {
                nint taskbar = FindWindow("Shell_TrayWnd", 0);
                nint tray = taskbar == 0 ? 0 : FindWindowEx(taskbar, 0, "TrayNotifyWnd", 0);
                if (tray != 0 && GetWindowRect(tray, out var rect) && rect.Left >= screen.Left + width + margin)
                    x = rect.Left - width - margin;
                else
                    return (x, work.Bottom - height - margin);
            }
            return (x, work.Bottom + (screen.Bottom - work.Bottom - height) / 2);
        }

        if (work.Top > screen.Top)
            return (screen.Left + margin, screen.Top + (work.Top - screen.Top - height) / 2);
        if (work.Left > screen.Left) return (work.Left + margin, screen.Bottom - height - margin);
        if (work.Right < screen.Right) return (work.Right - width - margin, screen.Bottom - height - margin);
        return (work.Left + margin, work.Bottom - height - margin);
    }

    internal static void KeepDockVisible(nint window, Rect screen, Rect work, int width, int height, double scale)
    {
        var (x, y) = Dock(screen, work, width, height, scale);
        var (safeX, safeY) = InsideWorkArea(work, x, y, width, height, Math.Max(2, (int)(4 * scale)));
        const int cloakedAttribute = 14;
        nint foreground = GetForegroundWindow();
        bool foregroundIsVisible = IsWindowVisible(foreground) &&
            (DwmGetWindowAttribute(foreground, cloakedAttribute, out uint cloaked, sizeof(uint)) != 0 || cloaked == 0);
        if (foregroundIsVisible && IsStartSurface(foreground))
        {
            PresentDocked(window, safeX, safeY);
            return;
        }

        PresentDocked(window, x, y);
        nint taskbar = FindWindow("Shell_TrayWnd", 0);
        if (taskbar != 0 && CoveredByWindow(window, taskbar))
            PresentDocked(window, safeX, safeY);
    }

    private static (int X, int Y) InsideWorkArea(Rect work, int x, int y, int width, int height, int margin)
    {
        int left = work.Left + margin;
        int top = work.Top + margin;
        return (
            Math.Clamp(x, left, Math.Max(left, work.Right - width - margin)),
            Math.Clamp(y, top, Math.Max(top, work.Bottom - height - margin)));
    }

    private static bool CoveredByWindow(nint window, nint coveringWindow)
    {
        if (!GetWindowRect(window, out var rect)) return false;
        var point = new Point { X = rect.Left + (rect.Right - rect.Left) / 2, Y = rect.Top + (rect.Bottom - rect.Top) / 2 };
        nint hit = WindowFromPoint(point);
        return hit == coveringWindow || (hit != 0 && GetAncestor(hit, 2) == coveringWindow);
    }

    internal static void SetToolWindow(nint window)
    {
        const int extendedStyle = -20;
        const int toolWindow = 0x80;
        SetWindowLong(window, extendedStyle, GetWindowLong(window, extendedStyle) | toolWindow);
    }

    internal static void PresentDocked(nint window, int x, int y)
    {
        const int showNormalWithoutActivation = 4;
        const uint noSize = 0x0001, noActivate = 0x0010, showWindow = 0x0040;
        if (IsIconic(window)) ShowWindow(window, showNormalWithoutActivation);
        SetWindowPos(window, -1, x, y, 0, 0, noSize | noActivate | showWindow);
    }

    internal static void KeepBehind(nint window) => SetWindowPos(window, 1, 0, 0, 0, 0, 0x13);

    internal static void Move(nint window, int x, int y) => SetWindowPos(window, 0, x, y, 0, 0, 0x15);
}
