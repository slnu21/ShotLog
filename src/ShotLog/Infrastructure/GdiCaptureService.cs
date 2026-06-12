using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace ShotLog.Infrastructure;

/// <summary>
/// GDI screen capture (Graphics.CopyFromScreen + PrintWindow). Works for the desktop and for
/// borderless/windowed games. Exclusive-fullscreen games can come back black — that case is handled
/// by the "capture now, annotate later in the Inbox" flow, and by a future WGC backend.
/// </summary>
public sealed class GdiCaptureService : ICaptureService
{
    public MonitorShot CaptureActiveMonitor()
    {
        var m = MonitorHelper.CursorMonitorPhysical();
        var bmp = new Bitmap(m.Width, m.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(m.Left, m.Top, 0, 0, new Size(m.Width, m.Height), CopyPixelOperation.SourceCopy);
        return new MonitorShot(bmp, m.Left, m.Top, m.Width, m.Height, m.Scale);
    }

    public Bitmap? CaptureActiveWindow()
    {
        IntPtr hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        if (!Native.GetWindowRect(hwnd, out var r)) return null;

        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        if (w <= 0 || h <= 0) return null;

        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        bool ok = false;
        using (var g = Graphics.FromImage(bmp))
        {
            IntPtr hdc = g.GetHdc();
            try { ok = Native.PrintWindow(hwnd, hdc, Native.PW_RENDERFULLCONTENT); }
            finally { g.ReleaseHdc(hdc); }
        }

        if (!ok)
        {
            // Fallback: straight screen copy of the window rect (loses occluded regions).
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(r.Left, r.Top, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
        }
        return bmp;
    }
}
