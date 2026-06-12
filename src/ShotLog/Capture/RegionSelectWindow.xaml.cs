using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ShotLog.Infrastructure;

namespace ShotLog.Capture;

/// <summary>
/// Full-monitor overlay that freezes the captured frame and lets the user drag a rectangle.
/// On release it crops that region (in physical pixels) from the source bitmap into <see cref="Result"/>.
/// </summary>
public partial class RegionSelectWindow : Window
{
    private readonly MonitorShot _shot;
    private Point _start;
    private bool _dragging;

    /// <summary>The cropped region (caller owns/disposes it), or null if cancelled.</summary>
    public System.Drawing.Bitmap? Result { get; private set; }

    public RegionSelectWindow(MonitorShot shot)
    {
        InitializeComponent();
        _shot = shot;

        // Span exactly the captured monitor, in DIPs.
        Left = shot.PxLeft / shot.Scale;
        Top = shot.PxTop / shot.Scale;
        Width = shot.PxWidth / shot.Scale;
        Height = shot.PxHeight / shot.Scale;

        var src = ImageHelper.ToBitmapSource(shot.Image);
        ShotDim.Source = src;
        ShotBright.Source = src;
        ShotBright.Clip = new RectangleGeometry(new Rect(0, 0, 0, 0)); // nothing revealed yet

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Cancel(); };
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _start = e.GetPosition(Surface);
        Surface.CaptureMouse();
        UpdateSelection(_start, _start);
        Sel.Visibility = Visibility.Visible;
        Hint.Visibility = Visibility.Collapsed;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        UpdateSelection(_start, e.GetPosition(Surface));
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        Surface.ReleaseMouseCapture();

        var end = e.GetPosition(Surface);
        var rect = MakeRect(_start, end);
        if (rect.Width < 4 || rect.Height < 4) { Cancel(); return; }

        try
        {
            double s = _shot.Scale;
            int x = (int)Math.Round(rect.X * s);
            int y = (int)Math.Round(rect.Y * s);
            int w = (int)Math.Round(rect.Width * s);
            int h = (int)Math.Round(rect.Height * s);

            x = Math.Clamp(x, 0, _shot.PxWidth - 1);
            y = Math.Clamp(y, 0, _shot.PxHeight - 1);
            w = Math.Clamp(w, 1, _shot.PxWidth - x);
            h = Math.Clamp(h, 1, _shot.PxHeight - y);

            var crop = new System.Drawing.Rectangle(x, y, w, h);
            Result = _shot.Image.Clone(crop, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            DialogResult = true;
        }
        catch
        {
            DialogResult = false;
        }
        Close();
    }

    private void UpdateSelection(Point a, Point b)
    {
        var r = MakeRect(a, b);
        Canvas.SetLeft(Sel, r.X);
        Canvas.SetTop(Sel, r.Y);
        Sel.Width = r.Width;
        Sel.Height = r.Height;
        ShotBright.Clip = new RectangleGeometry(r);
    }

    private static Rect MakeRect(Point a, Point b)
    {
        double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
        return new Rect(x, y, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    private void Cancel()
    {
        Result = null;
        DialogResult = false;
        Close();
    }
}
