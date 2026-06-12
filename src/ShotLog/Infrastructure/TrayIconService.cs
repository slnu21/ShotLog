using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("활성 모니터 캡처", null, (_, __) => CaptureMonitorRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem("캡처 + 메모", null, (_, __) => CaptureNoteRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem("영역 선택 캡처", null, (_, __) => CaptureRegionRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem("활성 창 캡처", null, (_, __) => CaptureWindowRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("인박스", null, (_, __) => InboxRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem("글쓰기 내보내기", null, (_, __) => ComposeRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("설정", null, (_, __) => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem("종료", null, (_, __) => ExitRequested?.Invoke(this, EventArgs.Empty)));
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
