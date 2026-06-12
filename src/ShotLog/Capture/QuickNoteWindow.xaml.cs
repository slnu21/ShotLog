using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ShotLog.Infrastructure;
using ShotLog.Models;
using ShotLog.Resources;

namespace ShotLog.Capture;

/// <summary>
/// Small corner card shown right after a capture: pick a preset destination, add tags + a memo,
/// then save. The memo/tags are written into a new <see cref="CaptureRecord"/> bound 1:1 to the PNG.
/// </summary>
public partial class QuickNoteWindow : Window
{
    private readonly System.Drawing.Bitmap _bmp;
    private readonly SettingsStore _settings;
    private readonly CaptureStore _captures;
    private readonly List<string> _tags = new();
    private readonly List<ToggleButton> _presetChips = new();
    private Preset _selected;
    private bool _saved;

    /// <summary>Raised after a successful save (so the Inbox can refresh).</summary>
    public event Action? Saved;

    public QuickNoteWindow(System.Drawing.Bitmap bmp, SettingsStore settings, CaptureStore captures)
    {
        InitializeComponent();
        _bmp = bmp;
        _settings = settings;
        _captures = captures;
        _selected = App.ActivePreset();

        Preview.Source = ImageHelper.ToBitmapSource(bmp);
        _tags.AddRange(_selected.DefaultTags);

        BuildPresetChips();
        RebuildTags();
        UpdateMapLabel();

        Loaded += OnLoaded;
        Closed += (_, __) => _bmp.Dispose();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var wa = MonitorHelper.CursorWorkAreaDip();
        Left = wa.Left + wa.Width - ActualWidth;
        Top = wa.Top + wa.Height - ActualHeight;
        MemoBox.Focus();
    }

    private void BuildPresetChips()
    {
        PresetPanel.Children.Clear();
        _presetChips.Clear();
        foreach (var p in _settings.Current.Presets)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new Ellipse
            {
                Width = 9, Height = 9, Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Fill = SafeBrush(p.Color),
            });
            sp.Children.Add(new TextBlock { Text = p.Name, VerticalAlignment = VerticalAlignment.Center });

            var chip = new ToggleButton
            {
                Style = (Style)FindResource("Chip"),
                Content = sp,
                Tag = p,
                IsChecked = p.Id == _selected.Id,
            };
            chip.Click += OnPresetChipClick;
            _presetChips.Add(chip);
            PresetPanel.Children.Add(chip);
        }
    }

    private void OnPresetChipClick(object sender, RoutedEventArgs e)
    {
        var chip = (ToggleButton)sender;
        _selected = (Preset)chip.Tag;
        foreach (var c in _presetChips) c.IsChecked = ReferenceEquals(c, chip);
        UpdateMapLabel();
    }

    private void UpdateMapLabel()
    {
        string stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        MapLabel.Text = $"🔗 {_selected.Name} · {stamp}.png" + (_settings.Current.SidecarEnabled ? " (+.md)" : "");
    }

    // ---- tags ----

    private void OnTagKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        AddTag(TagInput.Text);
        TagInput.Clear();
    }

    private void AddTag(string raw)
    {
        foreach (var t in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!_tags.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)))
                _tags.Add(t);
        }
        RebuildTags();
    }

    private void RebuildTags()
    {
        TagPanel.Children.Clear();
        if (_tags.Count == 0) { TagPanel.Visibility = Visibility.Collapsed; return; }
        TagPanel.Visibility = Visibility.Visible;
        foreach (var t in _tags)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text = t, Foreground = (Brush)FindResource("Tag"),
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            });
            var x = new TextBlock
            {
                Text = "✕", Margin = new Thickness(7, 0, 0, 0), Cursor = Cursors.Hand,
                Foreground = (Brush)FindResource("Tag"), FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            string captured = t;
            x.MouseLeftButtonUp += (_, __) => { _tags.Remove(captured); RebuildTags(); };
            sp.Children.Add(x);

            TagPanel.Children.Add(new Border
            {
                Background = (Brush)FindResource("TagBg"),
                BorderBrush = (Brush)FindResource("TagBorder"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(9, 4, 9, 4),
                Margin = new Thickness(0, 0, 8, 8),
                Child = sp,
            });
        }
    }

    // ---- save / discard ----

    private void OnMemoKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            Save();
        }
    }

    private void OnSave(object sender, RoutedEventArgs e) => Save();

    private void Save()
    {
        if (_saved) return;
        // flush any half-typed tag
        if (!string.IsNullOrWhiteSpace(TagInput.Text)) { AddTag(TagInput.Text); TagInput.Clear(); }

        try
        {
            var at = DateTimeOffset.Now;
            string path = CaptureIO.SavePng(_bmp, _selected.FolderPath, at);
            var rec = new CaptureRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                CapturedAt = at,
                ImagePath = path,
                PresetId = _selected.Id,
                PresetName = _selected.Name,
                Memo = MemoBox.Text.Trim(),
                Tags = new(_tags),
            };
            _captures.Add(rec);
            _captures.Save();
            if (_settings.Current.SidecarEnabled) CaptureIO.WriteSidecar(rec);

            _settings.Current.ActivePresetId = _selected.Id;
            _settings.Save();

            _saved = true;
            Saved?.Invoke();
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Strings.QuickNote_SaveFailed + ex.Message, "ShotLog", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDiscard(object sender, RoutedEventArgs e) => Close();

    private static Brush SafeBrush(string hex)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { return Brushes.SteelBlue; }
    }
}
