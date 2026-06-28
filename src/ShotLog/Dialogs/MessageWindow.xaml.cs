using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShotLog.Resources;

namespace ShotLog.Dialogs;

/// <summary>Severity of an <see cref="MessageWindow.Alert"/> — drives the header glyph and color.</summary>
public enum DialogKind { Info, Warn, Error }

/// <summary>
/// Dark-themed replacement for <see cref="System.Windows.MessageBox"/>. Use the static
/// <see cref="Confirm"/> / <see cref="Alert"/> helpers; they mirror the MessageBox call sites.
/// </summary>
public partial class MessageWindow : Window
{
    private MessageWindow(string message, string title, bool isConfirm, bool danger, string? okText, DialogKind kind)
    {
        InitializeComponent();

        TitleText.Text = string.IsNullOrEmpty(title) ? Strings.Dialog_Title : title;
        MessageText.Text = message;
        OkBtn.Content = okText ?? Strings.Common_OK;
        OkBtn.Style = (Style)FindResource(danger ? "BtnDanger" : "BtnPrimary");
        OkBtn.IsDefault = true;
        CancelBtn.Content = Strings.Common_Cancel;
        CancelBtn.Visibility = isConfirm ? Visibility.Visible : Visibility.Collapsed;

        (string glyph, string brushKey) = danger
            ? ("⚠", "Danger")                         // ⚠
            : kind switch
            {
                DialogKind.Warn => ("⚠", "Warn"),     // ⚠
                DialogKind.Error => ("⛔", "Danger"),  // ⛔
                _ => ("ℹ", "Accent"),                 // ℹ
            };
        IconText.Text = glyph;
        IconText.Foreground = (Brush)FindResource(brushKey);

        HeaderBar.MouseLeftButtonDown += (_, __) => DragMove();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
        Loaded += (_, __) => OkBtn.Focus();
    }

    private void OnOk(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void OnCancel(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    /// <summary>Modal yes/no confirmation. Returns true when the primary button is pressed.</summary>
    public static bool Confirm(Window? owner, string message, string title, bool danger = false, string? okText = null)
    {
        var w = new MessageWindow(message, title, isConfirm: true, danger, okText, DialogKind.Warn);
        Place(w, owner);
        return w.ShowDialog() == true;
    }

    /// <summary>Modal single-button notice (info / warning / error).</summary>
    public static void Alert(Window? owner, string message, string title, DialogKind kind = DialogKind.Info)
    {
        var w = new MessageWindow(message, title, isConfirm: false, danger: false, okText: null, kind);
        Place(w, owner);
        w.ShowDialog();
    }

    private static void Place(MessageWindow w, Window? owner)
    {
        if (owner != null && owner.IsLoaded) w.Owner = owner;
        else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }
}
