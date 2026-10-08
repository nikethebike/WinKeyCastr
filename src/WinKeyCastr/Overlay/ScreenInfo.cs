using System.Windows;
using WinKeyCastr.Native;

namespace WinKeyCastr.Overlay;

/// <summary>Monitor geometry in WPF units (assumes the primary monitor's scale factor, as WPF does).</summary>
internal static class ScreenInfo
{
    private static double Scale
    {
        get
        {
            var source = PresentationSourceScale();
            return source > 0 ? source : 1.0;
        }
    }

    private static double PresentationSourceScale()
    {
        // SystemParameters are already expressed in WPF units of the primary monitor.
        var physicalWidth = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Width ?? 0;
        return physicalWidth > 0 ? physicalWidth / SystemParameters.PrimaryScreenWidth : 1.0;
    }

    /// <summary>Work area (excluding the taskbar) of the monitor nearest to a point.</summary>
    public static Rect WorkAreaAt(Point point)
    {
        var scale = Scale;
        var monitor = Win32.MonitorFromPoint(new Win32.POINT { X = (int)(point.X * scale), Y = (int)(point.Y * scale) },
            Win32.MONITOR_DEFAULTTONEAREST);
        var info = new Win32.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFO>() };
        if (!Win32.GetMonitorInfo(monitor, ref info))
            return SystemParameters.WorkArea;
        var r = info.rcWork;
        return new Rect(r.Left / scale, r.Top / scale, (r.Right - r.Left) / scale, (r.Bottom - r.Top) / scale);
    }

    public static Rect PrimaryWorkArea => SystemParameters.WorkArea;

    public static Rect VirtualScreen => new(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
        SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);

    /// <summary>DPI scale of the monitor under a physical-pixel point.</summary>
    public static double ScaleAtPhysical(int x, int y)
    {
        var monitor = Win32.MonitorFromPoint(new Win32.POINT { X = x, Y = y }, Win32.MONITOR_DEFAULTTONEAREST);
        return Win32.GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 ? dpiX / 96.0 : 1.0;
    }
}
