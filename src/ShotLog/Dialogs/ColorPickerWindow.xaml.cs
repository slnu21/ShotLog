using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ShotLog.Infrastructure;
using WinForms = System.Windows.Forms;

namespace ShotLog.Dialogs;

/// <summary>Dark color picker: a curated swatch palette plus a "custom…" handoff to the OS color dialog.</summary>
public partial class ColorPickerWindow : Window
{
    private static readonly string[] Palette =
    {
        "#F85149", "#E3B341", "#3FB950", "#5AA0FF", "#7DEFD6", "#A371F7",
        "#F778BA", "#FF7B3D", "#58A6FF", "#FFFFFF", "#8B949E", "#21262D",
    };

    public string? Result { get; private set; }
    private readonly string _current;

    private ColorPickerWindow(string current)
    {
        InitializeComponent();
        WindowChrome.ApplyDarkTitleBar(this);
        _current = NormalizeHex(current);

        var selBrush = (Brush)FindResource("TextPrimary");
        foreach (var hex in Palette)
        {
            var border = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(7),
                Margin = new Thickness(3),
                Cursor = Cursors.Hand,
                Background = ToBrush(hex),
                BorderThickness = new Thickness(2),
                BorderBrush = SameColor(hex, current) ? selBrush : Brushes.Transparent,
                Tag = hex,
                ToolTip = hex,
            };
            border.MouseLeftButtonUp += (s, _) =>
            {
                Result = (string)((Border)s).Tag;
                DialogResult = true;
                Close();
            };
            SwatchPanel.Children.Add(border);
        }

        HeaderBar.MouseLeftButtonDown += (_, __) => DragMove();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
    }

    private void OnCustom(object sender, RoutedEventArgs e)
    {
        using var dlg = new WinForms.ColorDialog { FullOpen = true };
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(_current);
            dlg.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
        }
        catch { /* default */ }

        if (dlg.ShowDialog() == WinForms.DialogResult.OK)
        {
            Result = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
            DialogResult = true;
            Close();
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    private static Brush ToBrush(string hex)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { return Brushes.Gray; }
    }

    private static bool SameColor(string a, string b)
    {
        try { return (Color)ColorConverter.ConvertFromString(a) == (Color)ColorConverter.ConvertFromString(NormalizeHex(b)); }
        catch { return false; }
    }

    private static string NormalizeHex(string s) => string.IsNullOrWhiteSpace(s) ? "#5AA0FF" : s.Trim();

    /// <summary>Shows the picker seeded with <paramref name="current"/>; returns the chosen #RRGGBB or null.</summary>
    public static string? Pick(Window? owner, string current)
    {
        var w = new ColorPickerWindow(current);
        if (owner != null && owner.IsLoaded) w.Owner = owner;
        else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return w.ShowDialog() == true ? w.Result : null;
    }
}
