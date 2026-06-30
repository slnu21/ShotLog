using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ShotLog.Infrastructure;

/// <summary>Binds a <c>#RRGGBB</c> string to a <see cref="SolidColorBrush"/> for color swatches; falls back to Accent on parse failure.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    private static readonly Brush Fallback = new SolidColorBrush(Color.FromRgb(0x5A, 0xA0, 0xFF));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is string s && !string.IsNullOrWhiteSpace(s))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(s.Trim()));
        }
        catch { /* fall through */ }
        return Fallback;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>True → Collapsed, False → Visible. Complements <see cref="System.Windows.Controls.BooleanToVisibilityConverter"/>.</summary>
public sealed class InverseBoolVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
