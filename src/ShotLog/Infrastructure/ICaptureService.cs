using System.Drawing;

namespace ShotLog.Infrastructure;

/// <summary>A captured monitor frame plus the geometry needed to crop a sub-region from it.</summary>
public readonly record struct MonitorShot(
    Bitmap Image, int PxLeft, int PxTop, int PxWidth, int PxHeight, double Scale);

/// <summary>
/// Screen-capture backend. v1 is GDI (<see cref="GdiCaptureService"/>); a Windows.Graphics.Capture
/// backend for exclusive-fullscreen games can be slotted in behind this interface later.
/// </summary>
public interface ICaptureService
{
    /// <summary>Captures the whole monitor under the cursor.</summary>
    MonitorShot CaptureActiveMonitor();

    /// <summary>Captures the foreground window (PrintWindow), or null if there is none.</summary>
    Bitmap? CaptureActiveWindow();
}
