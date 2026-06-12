using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ShotLog.Infrastructure;
using ShotLog.Models;
using WinForms = System.Windows.Forms;

namespace ShotLog.Compose;

/// <summary>Filters captures by tag/preset/date, lets the user pick which to include, and exports portable Markdown.</summary>
public partial class ComposeWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly CaptureStore _captures;

    private readonly HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _presets = new();
    private string _dateMode = "all";
    private List<ComposeItemVM> _items = new();

    public ComposeWindow(SettingsStore settings, CaptureStore captures)
    {
        InitializeComponent();
        _settings = settings;
        _captures = captures;
        WindowChrome.ApplyDarkTitleBar(this);
    }

    public void ReloadData()
    {
        if (string.IsNullOrWhiteSpace(OutputBox.Text)) OutputBox.Text = App.ExportRoot();
        if (string.IsNullOrWhiteSpace(TitleBox.Text)) TitleBox.Text = $"{DateTimeOffset.Now:yyyy-MM-dd} 캡처 기록";

        BuildTagFilter();
        BuildPresetFilter();
        BuildDateFilter();
        ApplyFilter();
    }

    private void BuildTagFilter()
    {
        TagFilter.Children.Clear();
        var tags = _captures.Items.SelectMany(r => r.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToList();
        if (tags.Count == 0)
        {
            TagFilter.Children.Add(new TextBlock { Text = "(태그 없음)", Foreground = (System.Windows.Media.Brush)FindResource("TextDim"), FontSize = 12 });
            return;
        }
        foreach (var t in tags)
        {
            var chip = MakeChip(t, _tags.Contains(t));
            chip.Click += (s, _) =>
            {
                if (((ToggleButton)s).IsChecked == true) _tags.Add(t); else _tags.Remove(t);
                ApplyFilter();
            };
            TagFilter.Children.Add(chip);
        }
    }

    private void BuildPresetFilter()
    {
        PresetFilter.Children.Clear();
        foreach (var p in _settings.Current.Presets)
        {
            var chip = MakeChip(p.Name, _presets.Contains(p.Id));
            chip.Click += (s, _) =>
            {
                if (((ToggleButton)s).IsChecked == true) _presets.Add(p.Id); else _presets.Remove(p.Id);
                ApplyFilter();
            };
            PresetFilter.Children.Add(chip);
        }
    }

    private void BuildDateFilter()
    {
        DateFilter.Children.Clear();
        AddDateChip("전체", "all");
        AddDateChip("오늘", "today");
        AddDateChip("최근 7일", "week");
    }

    private void AddDateChip(string label, string mode)
    {
        var chip = MakeChip(label, _dateMode == mode);
        chip.Click += (s, _) =>
        {
            _dateMode = mode;
            foreach (var c in DateFilter.Children.OfType<ToggleButton>())
                c.IsChecked = (string)c.Tag == mode;
            ApplyFilter();
        };
        chip.Tag = mode;
        DateFilter.Children.Add(chip);
    }

    private ToggleButton MakeChip(string label, bool on) => new()
    {
        Style = (Style)FindResource("Chip"),
        Content = label,
        IsChecked = on,
    };

    private void ApplyFilter()
    {
        var now = DateTimeOffset.Now;
        var candidates = _captures.Items.Where(r =>
            (_tags.Count == 0 || r.Tags.Any(t => _tags.Contains(t))) &&
            (_presets.Count == 0 || _presets.Contains(r.PresetId)) &&
            DateMatch(r, now))
            .OrderBy(r => r.CapturedAt)
            .ToList();

        _items = candidates.Select(r => new ComposeItemVM(r)).ToList();
        foreach (var vm in _items) vm.SelectionChanged += OnSelectionChanged;
        List.ItemsSource = _items;
        UpdatePreview();
    }

    private bool DateMatch(CaptureRecord r, DateTimeOffset now) => _dateMode switch
    {
        "today" => r.CapturedAt.LocalDateTime.Date == now.LocalDateTime.Date,
        "week" => r.CapturedAt >= now.AddDays(-7),
        _ => true,
    };

    private void OnSelectionChanged() => UpdatePreview();
    private void OnAnyChanged(object sender, RoutedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        var selected = _items.Where(i => i.Selected).Select(i => i.Record).ToList();
        CountLabel.Text = $"포함할 캡처 · 시간순 ({selected.Count}/{_items.Count})";
        PreviewBox.Text = MarkdownExporter.BuildPreview(selected, TitleBox.Text, FrontMatterBox.IsChecked == true);
        GenerateBtn.IsEnabled = selected.Count > 0;
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        using var dlg = new WinForms.FolderBrowserDialog();
        if (!string.IsNullOrWhiteSpace(OutputBox.Text)) dlg.SelectedPath = OutputBox.Text;
        if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            OutputBox.Text = dlg.SelectedPath;
    }

    private void OnGenerate(object sender, RoutedEventArgs e)
    {
        var selected = _items.Where(i => i.Selected).Select(i => i.Record).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "포함할 캡처를 하나 이상 선택하세요.", "ShotLog", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string outputRoot = string.IsNullOrWhiteSpace(OutputBox.Text) ? App.ExportRoot() : OutputBox.Text.Trim();
        OutputBox.Text = outputRoot;

        try
        {
            var res = MarkdownExporter.Export(selected, TitleBox.Text.Trim(), outputRoot, FrontMatterBox.IsChecked == true);
            StatusLabel.Text = $"생성됨: {res.MarkdownPath} · 이미지 {res.ImageCount}장";
            Process.Start(new ProcessStartInfo(res.MarkdownPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "내보내기 실패: " + ex.Message, "ShotLog", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
