using System;
using System.Runtime.InteropServices;

namespace ShotLog.Infrastructure;

/// <summary>Cursor-monitor geometry helpers (physical pixels for capture, DIPs for window placement).</summary>
public static class MonitorHelper
{
    /// <summary>Full bounds of the monitor under the cursor, in PHYSICAL pixels, plus its DPI scale.</summary>
    public readonly record struct MonRect(int Left, int Top, int Width, int Height, double Scale);

    public static MonRect CursorMonitorPhysical()
    {
        Native.GetCursorPos(out var p);
        IntPtr mon = Native.MonitorFromPoint(p, Native.MONITOR_DEFAULTTONEAREST);
        var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
        if (mon != IntPtr.Zero && Native.GetMonitorInfo(mon, ref mi))
        {
            double scale = DpiScale(mon);
            var r = mi.rcMonitor;
            return new MonRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, scale);
        }
        return new MonRect(0, 0, 1920, 1080, 1.0);
    }

    /// <summary>Work area of the monitor under the cursor, in device-independent pixels (WPF Left/Top/Width/Height).</summary>
    public static (double Left, double Top, double Width, double Height) CursorWorkAreaDip()
    {
        Native.GetCursorPos(out var p);
        IntPtr mon = Native.MonitorFromPoint(p, Native.MONITOR_DEFAULTTONEAREST);
        var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
        if (mon != IntPtr.Zero && Native.GetMonitorInfo(mon, ref mi))
        {
            double s = DpiScale(mon);
            var w = mi.rcWork;
            return (w.Left / s, w.Top / s, (w.Right - w.Left) / s, (w.Bottom - w.Top) / s);
        }
        return (0, 0, 1920, 1040);
    }

    private static double DpiScale(IntPtr mon)
    {
        if (Native.GetDpiForMonitor(mon, Native.MonitorDpiType.EffectiveDpi, out uint dpiX, out _) == 0 && dpiX > 0)
            return dpiX / 96.0;
        return 1.0;
    }
}
