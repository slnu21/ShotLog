using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ShotLog.Resources;

namespace ShotLog.Infrastructure;

/// <summary>
/// Tray presence for ShotLog. Built on the WinForms <see cref="NotifyIcon"/> (zero NuGet dependency);
/// the icon is generated in-code as a small aperture mark. Menu items raise events the App wires up.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    private readonly NotifyIcon _icon;
    private readonly Icon _generated;

    public event EventHandler? CaptureMonitorRequested;
    public event EventHandler? CaptureNoteRequested;
    public event EventHandler? CaptureRegionRequested;
    public event EventHandler? CaptureWindowRequested;
    public event EventHandler? InboxRequested;
    public event EventHandler? ComposeRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService()
    {
        _generated = BuildIcon();
        _icon = new NotifyIcon
        {
            Text = "ShotLog",
            Visible = false,
            Icon = _generated,
            ContextMenuStrip = BuildMenu(),
        };
        // Left-click → capture+memo (the deliberate, interactive path).
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) CaptureNoteRequested?.Invoke(this, EventArgs.Empty);
        };
    }

    public void Show() => _icon.Visible = true;

    public void Notify(string title, string text)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = text;
        _icon.ShowBalloonTip(2500);
    }

    /// <summary>Rebuilds the context menu with current resource strings (e.g. after a language change).</summary>
    public void RebuildMenu()
    {
        var old = _icon.ContextMenuStrip;
        _icon.ContextMenuStrip = BuildMenu();
        old?.Dispose();
    }

    private ContextMenuStrip BuildMenu()
    {
        // '&' is a mnemonic prefix in WinForms menus; double it so it renders literally.
        static string M(string s) => s.Replace("&", "&&");
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem(M(Strings.Tray_CaptureMonitor), null, (_, __) => CaptureMonitorRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem(M(Strings.Tray_CaptureNote), null, (_, __) => CaptureNoteRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem(M(Strings.Tray_CaptureRegion), null, (_, __) => CaptureRegionRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem(M(Strings.Tray_CaptureWindow), null, (_, __) => CaptureWindowRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(M(Strings.Tray_Inbox), null, (_, __) => InboxRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem(M(Strings.Tray_Compose), null, (_, __) => ComposeRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(M(Strings.Tray_Settings), null, (_, __) => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem(M(Strings.Tray_Exit), null, (_, __) => ExitRequested?.Invoke(this, EventArgs.Empty)));
        return menu;
    }

    /// <summary>Builds the tray icon from the shared <see cref="AppIconFactory"/> mark (32px).</summary>
    private static Icon BuildIcon()
    {
        using var bmp = AppIconFactory.Render(32);
        IntPtr hicon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hicon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hicon);
        }
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _generated.Dispose();
    }
}
