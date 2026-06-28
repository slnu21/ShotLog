using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ShotLog.Infrastructure;
using ShotLog.Resources;
using ShapePath = System.Windows.Shapes.Path;

namespace ShotLog.Capture;

/// <summary>
/// Lightweight image annotator: freehand pen + highlighter (InkCanvas strokes), arrows / rectangles /
/// text boxes (InkCanvas children). Reads the input bitmap (never disposes it) and, on Done, bakes the
/// composited surface into a brand-new <see cref="System.Drawing.Bitmap"/> exposed via <see cref="Result"/>.
/// </summary>
public partial class AnnotationWindow : Window
{
    private enum Tool { Select, Pen, Highlighter, Arrow, Rect, Text }

    private readonly int _pxW;
    private readonly int _pxH;
    private readonly Stack<Action> _undo = new();
    private readonly List<ToggleButton> _toolButtons = new();
    private readonly List<Border> _swatches = new();

    private Tool _tool = Tool.Pen;
    private Color _color = Color.FromRgb(0xF8, 0x51, 0x49);   // red
    private double _width = 4;

    // shape drag state
    private bool _drawing;
    private Point _start;
    private Shape? _preview;

    /// <summary>The annotated image. Null until Done; ownership transfers to the caller.</summary>
    public System.Drawing.Bitmap? Result { get; private set; }

    public AnnotationWindow(System.Drawing.Bitmap source)
    {
        InitializeComponent();
        WindowChrome.ApplyDarkTitleBar(this);

        _pxW = source.Width;
        _pxH = source.Height;

        var img = ImageHelper.ToBitmapSource(source);   // frozen copy; source is not retained/disposed here
        Surface.Width = _pxW;
        Surface.Height = _pxH;
        Surface.Background = new ImageBrush(img) { Stretch = Stretch.Fill };

        CancelBtn.Content = Strings.Common_Cancel;
        DoneBtn.Content = Strings.Common_Done;

        BuildToolbar();
        ApplyTool();

        Surface.StrokeCollected += (_, e) => _undo.Push(() => Surface.Strokes.Remove(e.Stroke));
        Surface.PreviewMouseLeftButtonDown += OnSurfaceDown;
        Surface.MouseMove += OnSurfaceMove;
        Surface.PreviewMouseLeftButtonUp += OnSurfaceUp;
        KeyDown += OnKeyDown;

        SizeToImage();
    }

    private void SizeToImage()
    {
        var wa = SystemParameters.WorkArea;
        Width = Math.Min(wa.Width * 0.95, _pxW + 56);
        Height = Math.Min(wa.Height * 0.95, _pxH + 170);
    }

    // ---- toolbar ----

    private void BuildToolbar()
    {
        AddToolButton("↖", Strings.Annot_Select, Tool.Select);   // ↖
        AddToolButton("✎", Strings.Annot_Pen, Tool.Pen);         // ✎
        AddToolButton("▬", Strings.Annot_Highlighter, Tool.Highlighter); // ▬
        AddToolButton("↗", Strings.Annot_Arrow, Tool.Arrow);     // ↗
        AddToolButton("▭", Strings.Annot_Rect, Tool.Rect);       // ▭
        AddToolButton("T", Strings.Annot_Text, Tool.Text);

        AddSeparator();
        foreach (var c in new[]
        {
            Color.FromRgb(0xF8,0x51,0x49), Color.FromRgb(0xE3,0xB3,0x41), Color.FromRgb(0x3F,0xB9,0x50),
            Color.FromRgb(0x5A,0xA0,0xFF), Color.FromRgb(0xFF,0xFF,0xFF), Color.FromRgb(0x10,0x14,0x18),
        }) AddSwatch(c);

        AddSeparator();
        AddWidthButton(Strings.Annot_Width + " S", 2.5);
        AddWidthButton(Strings.Annot_Width + " M", 4, isDefault: true);
        AddWidthButton(Strings.Annot_Width + " L", 7);

        AddSeparator();
        AddCommandButton(Strings.Annot_Undo, OnUndo);
        AddCommandButton(Strings.Annot_Clear, OnClear);
    }

    private void AddToolButton(string glyph, string tip, Tool tool)
    {
        var b = new ToggleButton
        {
            Style = (Style)FindResource("Chip"),
            Content = glyph,
            ToolTip = tip,
            IsChecked = tool == _tool,
            MinWidth = 38,
        };
        b.Click += (_, __) => { _tool = tool; SyncToolButtons(b); ApplyTool(); };
        _toolButtons.Add(b);
        ToolBar.Children.Add(b);
    }

    private void SyncToolButtons(ToggleButton active)
    {
        foreach (var b in _toolButtons) b.IsChecked = ReferenceEquals(b, active);
    }

    private void AddSwatch(Color c)
    {
        var border = new Border
        {
            Width = 22, Height = 22, CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(c), Cursor = Cursors.Hand,
            Margin = new Thickness(2, 0, 2, 0), BorderThickness = new Thickness(2),
            BorderBrush = c == _color ? (Brush)FindResource("TextPrimary") : Brushes.Transparent,
        };
        border.MouseLeftButtonUp += (_, __) =>
        {
            _color = c;
            foreach (var s in _swatches)
                s.BorderBrush = ((SolidColorBrush)s.Background).Color == c ? (Brush)FindResource("TextPrimary") : Brushes.Transparent;
            ApplyTool();
        };
        _swatches.Add(border);
        ToolBar.Children.Add(border);
    }

    private void AddWidthButton(string label, double width, bool isDefault = false)
    {
        var b = new ToggleButton
        {
            Style = (Style)FindResource("Chip"),
            Content = label,
            IsChecked = isDefault,
        };
        b.Click += (_, __) =>
        {
            _width = width;
            foreach (var child in ToolBar.Children)
                if (child is ToggleButton tb && tb.Tag as string == "w") tb.IsChecked = ReferenceEquals(tb, b);
            ApplyTool();
        };
        b.Tag = "w";
        ToolBar.Children.Add(b);
    }

    private void AddCommandButton(string label, RoutedEventHandler onClick)
    {
        var b = new Button { Style = (Style)FindResource("Btn"), Content = label, Padding = new Thickness(11, 7, 11, 7), Margin = new Thickness(0, 0, 8, 0) };
        b.Click += onClick;
        ToolBar.Children.Add(b);
    }

    private void AddSeparator() => ToolBar.Children.Add(new Border
    {
        Width = 1, Margin = new Thickness(6, 2, 8, 2),
        Background = (Brush)FindResource("Border"),
    });

    private void ApplyTool()
    {
        switch (_tool)
        {
            case Tool.Select:
                Surface.EditingMode = InkCanvasEditingMode.Select;
                break;
            case Tool.Pen:
                Surface.EditingMode = InkCanvasEditingMode.Ink;
                Surface.DefaultDrawingAttributes = new DrawingAttributes
                {
                    Color = _color, Width = _width, Height = _width, FitToCurve = true,
                };
                break;
            case Tool.Highlighter:
                Surface.EditingMode = InkCanvasEditingMode.Ink;
                Surface.DefaultDrawingAttributes = new DrawingAttributes
                {
                    Color = _color, Width = _width * 4, Height = _width * 4,
                    IsHighlighter = true, StylusTip = StylusTip.Rectangle, IgnorePressure = true,
                };
                break;
            default: // Arrow / Rect / Text → manual mouse handling
                Surface.EditingMode = InkCanvasEditingMode.None;
                break;
        }
    }

    // ---- manual shape / text drawing ----

    private void OnSurfaceDown(object sender, MouseButtonEventArgs e)
    {
        if (_tool == Tool.Text)
        {
            if (e.OriginalSource is TextBox) return;   // let an existing box take the click
            AddTextBox(e.GetPosition(Surface));
            e.Handled = true;
            return;
        }
        if (_tool != Tool.Arrow && _tool != Tool.Rect) return;

        _start = e.GetPosition(Surface);
        _drawing = true;
        _preview = _tool == Tool.Rect
            ? new Rectangle { Stroke = new SolidColorBrush(_color), StrokeThickness = _width, Fill = Brushes.Transparent }
            : new ShapePath { Stroke = new SolidColorBrush(_color), StrokeThickness = _width, Fill = new SolidColorBrush(_color), StrokeLineJoin = PenLineJoin.Round };
        if (_tool == Tool.Rect) { InkCanvas.SetLeft(_preview, _start.X); InkCanvas.SetTop(_preview, _start.Y); }
        Surface.Children.Add(_preview);
        Surface.CaptureMouse();
        e.Handled = true;
    }

    private void OnSurfaceMove(object sender, MouseEventArgs e)
    {
        if (!_drawing || _preview == null) return;
        var p = e.GetPosition(Surface);
        if (_preview is Rectangle r)
        {
            double x = Math.Min(_start.X, p.X), y = Math.Min(_start.Y, p.Y);
            InkCanvas.SetLeft(r, x); InkCanvas.SetTop(r, y);
            r.Width = Math.Abs(p.X - _start.X); r.Height = Math.Abs(p.Y - _start.Y);
        }
        else if (_preview is ShapePath path)
        {
            path.Data = BuildArrow(_start, p, _width);
        }
    }

    private void OnSurfaceUp(object sender, MouseButtonEventArgs e)
    {
        if (!_drawing || _preview == null) return;
        _drawing = false;
        Surface.ReleaseMouseCapture();

        var p = e.GetPosition(Surface);
        bool tiny = Math.Abs(p.X - _start.X) < 4 && Math.Abs(p.Y - _start.Y) < 4;
        var shape = _preview;
        _preview = null;
        if (tiny) { Surface.Children.Remove(shape); return; }
        _undo.Push(() => Surface.Children.Remove(shape));
    }

    private void AddTextBox(Point at)
    {
        var tb = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(_color),
            CaretBrush = new SolidColorBrush(_color),
            FontSize = Math.Max(15, _width * 5),
            FontWeight = FontWeights.SemiBold,
            FontFamily = (FontFamily)FindResource("UiFont"),
            MinWidth = 48,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Tag = "annot-text",
        };
        InkCanvas.SetLeft(tb, at.X);
        InkCanvas.SetTop(tb, at.Y);
        Surface.Children.Add(tb);
        _undo.Push(() => Surface.Children.Remove(tb));
        tb.Focus();
    }

    private static Geometry BuildArrow(Point s, Point e, double width)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(s, false, false);
            ctx.LineTo(e, true, true);

            var dir = e - s;
            if (dir.Length > 1)
            {
                dir.Normalize();
                var perp = new Vector(-dir.Y, dir.X);
                double head = Math.Max(11, width * 3.5);
                double half = head * 0.55;
                Point b1 = e - dir * head + perp * half;
                Point b2 = e - dir * head - perp * half;
                ctx.BeginFigure(e, true, true);
                ctx.LineTo(b1, true, true);
                ctx.LineTo(b2, true, true);
            }
        }
        g.Freeze();
        return g;
    }

    // ---- commands ----

    private void OnUndo(object sender, RoutedEventArgs e)
    {
        if (_undo.Count > 0) _undo.Pop()();
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        Surface.Strokes.Clear();
        Surface.Children.Clear();
        _undo.Clear();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { OnUndo(sender, e); e.Handled = true; }
        else if (e.Key == Key.Escape) { DialogResult = false; Close(); }
    }

    // ---- finish ----

    private void OnCancel(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    private void OnDone(object sender, RoutedEventArgs e)
    {
        // Drop any active selection/caret so adorners don't bake into the image.
        Surface.Select(new StrokeCollection(), Array.Empty<UIElement>());
        Surface.EditingMode = InkCanvasEditingMode.None;
        DoneBtn.Focus();
        Surface.UpdateLayout();

        try { Result = Bake(); DialogResult = true; }
        catch { DialogResult = false; }
        Close();
    }

    private System.Drawing.Bitmap Bake()
    {
        var rtb = new RenderTargetBitmap(_pxW, _pxH, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(Surface);

        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        enc.Save(ms);
        ms.Position = 0;
        using var loaded = new System.Drawing.Bitmap(ms);
        return new System.Drawing.Bitmap(loaded);   // independent copy, safe after the stream is gone
    }
}
