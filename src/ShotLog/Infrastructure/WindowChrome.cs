using System.Windows;
using System.Windows.Interop;

namespace ShotLog.Infrastructure;

/// <summary>Applies the Win11 dark title bar to a standard WPF window so native chrome matches the dark UI.</summary>
public static class WindowChrome
{
    public static void ApplyDarkTitleBar(Window w)
    {
        w.SourceInitialized += (_, __) =>
        {
            try
            {
                var h = new WindowInteropHelper(w).Handle;
                int on = 1;
                Native.DwmSetWindowAttribute(h, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
            }
            catch { /* pre-20H1 has no dark title bar; ignore */ }
        };
    }
}
