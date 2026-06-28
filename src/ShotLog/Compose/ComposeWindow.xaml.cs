using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using ShotLog.Dialogs;
using ShotLog.Infrastructure;
using ShotLog.Models;
using ShotLog.Resources;
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

    // Rendered (WebView2) preview state. The data-URI cache keeps title keystrokes from re-encoding images.
    private readonly Dictionary<string, string?> _dataUriCache = new();
    private readonly DispatcherTimer _renderTimer;
    private bool _webReady;
    private int _renderSeq;
    private string? _previewFile;

    public ComposeWindow(SettingsStore settings, CaptureStore captures)
    {
        InitializeComponent();
        _settings = settings;
        _captures = captures;
        WindowChrome.ApplyDarkTitleBar(this);

        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        _renderTimer.Tick += (_, __) => { _renderTimer.Stop(); RenderHtmlPreview(); };
        Loaded += async (_, __) => await InitWebViewAsync();
        Closed += (_, __) => CleanupWebView();
    }

    public void ReloadData()
    {
        _dataUriCache.Clear();   // images may have changed (e.g. annotated) since last open
        if (string.IsNullOrWhiteSpace(OutputBox.Text)) OutputBox.Text = App.ExportRoot();
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
            TitleBox.Text = string.Format(Strings.Compose_DefaultTitleFormat, DateTimeOffset.Now.ToString("yyyy-MM-dd"));

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
            TagFilter.Children.Add(new TextBlock { Text = Strings.Compose_NoTags, Foreground = (System.Windows.Media.Brush)FindResource("TextDim"), FontSize = 12 });
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
        AddDateChip(Strings.Common_All, "all");
        AddDateChip(Strings.Compose_Today, "today");
        AddDateChip(Strings.Compose_Last7, "week");
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
        CountLabel.Text = string.Format(Strings.Compose_CountFormat, selected.Count, _items.Count);
        PreviewBox.Text = MarkdownExporter.BuildPreview(selected, TitleBox.Text, FrontMatterBox.IsChecked == true);
        GenerateBtn.IsEnabled = CopyMdBtn.IsEnabled = HtmlBtn.IsEnabled = selected.Count > 0;
        QueueRender();
    }

    // ---- rendered (WebView2) preview ----

    private static string WebViewDir() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShotLog", "webview2");

    private async Task InitWebViewAsync()
    {
        try
        {
            string udf = WebViewDir();
            Directory.CreateDirectory(udf);
            var env = await CoreWebView2Environment.CreateAsync(null, udf);
            await RenderView.EnsureCoreWebView2Async(env);
            var s = RenderView.CoreWebView2.Settings;
            s.AreDevToolsEnabled = false;
            s.AreDefaultContextMenusEnabled = false;
            s.IsStatusBarEnabled = false;
            s.IsZoomControlEnabled = false;
            _webReady = true;
            RenderHtmlPreview();
        }
        catch
        {
            // WebView2 runtime missing or init failed → keep the text preview, show a hint.
            RenderView.Visibility = Visibility.Collapsed;
            WebMissingHint.Visibility = Visibility.Visible;
        }
    }

    private void QueueRender()
    {
        if (_renderTimer == null) return;
        _renderTimer.Stop();
        _renderTimer.Start();
    }

    private string? DataUriFor(string path)
    {
        if (_dataUriCache.TryGetValue(path, out var v)) return v;
        v = ImageHelper.ToDataUri(path, 900);
        _dataUriCache[path] = v;
        return v;
    }

    private void RenderHtmlPreview()
    {
        if (!_webReady) return;
        try
        {
            var selected = _items.Where(i => i.Selected).Select(i => i.Record).ToList();
            string html = MarkdownExporter.BuildHtmlPreview(selected, TitleBox.Text, FrontMatterBox.IsChecked == true, DataUriFor);

            // Navigate to a fresh temp file rather than NavigateToString (which caps at ~2 MB; base64 images blow past it).
            string dir = Path.Combine(WebViewDir(), "tmp");
            Directory.CreateDirectory(dir);
            string? prev = _previewFile;
            _previewFile = Path.Combine(dir, $"preview-{_renderSeq++}.html");
            File.WriteAllText(_previewFile, html, new UTF8Encoding(false));
            RenderView.CoreWebView2.Navigate(new Uri(_previewFile).AbsoluteUri);
            if (prev != null) { try { File.Delete(prev); } catch { /* best-effort */ } }
        }
        catch { /* preview is best-effort, never blocks export */ }
    }

    private void CleanupWebView()
    {
        try { RenderView.Dispose(); } catch { }
        try { if (_previewFile != null && File.Exists(_previewFile)) File.Delete(_previewFile); } catch { }
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        using var dlg = new WinForms.FolderBrowserDialog();
        if (!string.IsNullOrWhiteSpace(OutputBox.Text)) dlg.SelectedPath = OutputBox.Text;
        if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            OutputBox.Text = dlg.SelectedPath;
    }

    private void OnCopyMarkdown(object sender, RoutedEventArgs e)
    {
        var selected = _items.Where(i => i.Selected).Select(i => i.Record).ToList();
        if (selected.Count == 0) return;
        try
        {
            Clipboard.SetText(MarkdownExporter.BuildPreview(selected, TitleBox.Text, FrontMatterBox.IsChecked == true));
            StatusLabel.Text = Strings.Compose_CopiedStatus;
        }
        catch { /* clipboard busy — ignore */ }
    }

    private void OnExportHtml(object sender, RoutedEventArgs e)
    {
        var selected = _items.Where(i => i.Selected).Select(i => i.Record).ToList();
        if (selected.Count == 0) return;

        string outputRoot = string.IsNullOrWhiteSpace(OutputBox.Text) ? App.ExportRoot() : OutputBox.Text.Trim();
        OutputBox.Text = outputRoot;
        try
        {
            string path = MarkdownExporter.ExportHtml(selected, TitleBox.Text.Trim(), outputRoot,
                FrontMatterBox.IsChecked == true, p => ImageHelper.ToDataUri(p, 1920));
            StatusLabel.Text = string.Format(Strings.Compose_HtmlGeneratedFormat, path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageWindow.Alert(this, Strings.Compose_ExportFailed + ex.Message, Strings.Dialog_Title, DialogKind.Error);
        }
    }

    private void OnGenerate(object sender, RoutedEventArgs e)
    {
        var selected = _items.Where(i => i.Selected).Select(i => i.Record).ToList();
        if (selected.Count == 0)
        {
            MessageWindow.Alert(this, Strings.Compose_SelectAtLeastOne, Strings.Dialog_Title, DialogKind.Info);
            return;
        }

        string outputRoot = string.IsNullOrWhiteSpace(OutputBox.Text) ? App.ExportRoot() : OutputBox.Text.Trim();
        OutputBox.Text = outputRoot;

        try
        {
            var res = MarkdownExporter.Export(selected, TitleBox.Text.Trim(), outputRoot, FrontMatterBox.IsChecked == true);
            StatusLabel.Text = string.Format(Strings.Compose_GeneratedStatusFormat, res.MarkdownPath, res.ImageCount);
            Process.Start(new ProcessStartInfo(res.MarkdownPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageWindow.Alert(this, Strings.Compose_ExportFailed + ex.Message, Strings.Dialog_Title, DialogKind.Error);
        }
    }
}
