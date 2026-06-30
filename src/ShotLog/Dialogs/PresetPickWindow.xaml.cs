using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ShotLog.Infrastructure;
using ShotLog.Models;

namespace ShotLog.Dialogs;

/// <summary>Dark modal that picks a destination preset for "move to preset" and whether to replace tags.</summary>
public partial class PresetPickWindow : Window
{
    public Preset? SelectedPreset { get; private set; }
    public bool ReplaceTags { get; private set; }

    private PresetPickWindow(IEnumerable<Preset> presets, bool initialReplaceTags)
    {
        InitializeComponent();
        WindowChrome.ApplyDarkTitleBar(this);

        var fg = (Brush)FindResource("TextPrimary");
        bool first = true;
        foreach (var p in presets)
        {
            var rb = new RadioButton
            {
                Content = p.Name,
                Tag = p,
                Foreground = fg,
                GroupName = "preset",
                IsChecked = first,
                Margin = new Thickness(6, 5, 6, 5),
            };
            PresetPanel.Children.Add(rb);
            first = false;
        }

        ReplaceTagsCheck.IsChecked = initialReplaceTags;
        HeaderBar.MouseLeftButtonDown += (_, __) => DragMove();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
        Loaded += (_, __) => OkBtn.Focus();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var chosen = PresetPanel.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true);
        SelectedPreset = chosen?.Tag as Preset;
        ReplaceTags = ReplaceTagsCheck.IsChecked == true;
        DialogResult = SelectedPreset != null;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    /// <summary>Shows the picker; returns the chosen preset + replace-tags flag, or null when cancelled.</summary>
    public static (Preset Preset, bool ReplaceTags)? Pick(Window? owner, IEnumerable<Preset> presets, bool initialReplaceTags)
    {
        var w = new PresetPickWindow(presets, initialReplaceTags);
        if (owner != null && owner.IsLoaded) w.Owner = owner;
        else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return w.ShowDialog() == true && w.SelectedPreset != null
            ? (w.SelectedPreset, w.ReplaceTags)
            : null;
    }
}
