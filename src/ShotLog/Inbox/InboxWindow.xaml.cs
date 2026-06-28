using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ShotLog.Capture;
using ShotLog.Dialogs;
using ShotLog.Infrastructure;
using ShotLog.Resources;

namespace ShotLog.Inbox;

/// <summary>Recent captures with inline memo/tag editing, search, preset filter and delete.</summary>
public partial class InboxWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly CaptureStore _captures;
    private List<InboxItemVM> _all = new();
    private string? _presetFilterId;

    /// <summary>Raised when the user clicks "글쓰기 내보내기".</summary>
    public event Action? ComposeRequested;

    public InboxWindow(SettingsStore settings, CaptureStore captures)
    {
        InitializeComponent();
        _settings = settings;
        _captures = captures;
        WindowChrome.ApplyDarkTitleBar(this);
    }

    public void ReloadList()
    {
        _all = _captures.Items
            .OrderByDescending(r => r.CapturedAt)
            .Select(r => new InboxItemVM(r, _captures, _settings))
            .ToList();
        BuildPresetFilter();
        ApplyFilter();
    }

    public void ReloadIfVisible()
    {
        if (IsVisible) ReloadList();
    }

    private void BuildPresetFilter()
    {
        PresetFilter.Children.Clear();
        AddFilterChip(Strings.Common_All, null);
        foreach (var p in _settings.Current.Presets)
            AddFilterChip(p.Name, p.Id);
    }

    private void AddFilterChip(string label, string? id)
    {
        var chip = new ToggleButton
        {
            Style = (Style)FindResource("Chip"),
            Content = label,
            Tag = id,
            IsChecked = _presetFilterId == id,
        };
        chip.Click += (s, _) =>
        {
            _presetFilterId = (string?)((ToggleButton)s).Tag;
            foreach (var c in PresetFilter.Children.OfType<ToggleButton>())
                c.IsChecked = (string?)c.Tag == _presetFilterId;
            ApplyFilter();
        };
        PresetFilter.Children.Add(chip);
    }

    private void ApplyFilter()
    {
        string q = SearchBox.Text;
        var view = _all.Where(vm =>
            (_presetFilterId == null || vm.Record.PresetId == _presetFilterId) && vm.Matches(q)).ToList();
        List.ItemsSource = view;
        EmptyHint.Visibility = view.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Text = _all.Count == 0 ? Strings.Inbox_Empty : Strings.Inbox_NoMatch;
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void OnRefresh(object sender, RoutedEventArgs e) => ReloadList();
    private void OnCompose(object sender, RoutedEventArgs e) => ComposeRequested?.Invoke();

    private void OnOpenImage(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not InboxItemVM vm) return;
        try
        {
            if (File.Exists(vm.ImagePath))
                Process.Start(new ProcessStartInfo(vm.ImagePath) { UseShellExecute = true });
        }
        catch { /* ignore */ }
    }

    private void OnAnnotate(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not InboxItemVM vm) return;
        if (!File.Exists(vm.ImagePath)) return;

        System.Drawing.Bitmap src;
        try
        {
            // Copy out of a temp bitmap so the PNG file handle is released immediately (allows overwrite).
            using var tmp = new System.Drawing.Bitmap(vm.ImagePath);
            src = new System.Drawing.Bitmap(tmp);
        }
        catch { return; }

        using (src)
        {
            var win = new AnnotationWindow(src) { Owner = this };
            if (win.ShowDialog() == true && win.Result != null)
            {
                using var result = win.Result;
                try { CaptureIO.OverwritePng(result, vm.ImagePath); }
                catch { return; }
                ReloadList();   // rebuilds thumbnails (LoadThumb ignores the image cache)
            }
        }
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not InboxItemVM vm) return;
        if (!File.Exists(vm.ImagePath)) return;
        try
        {
            using var bmp = new System.Drawing.Bitmap(vm.ImagePath);
            ClipboardHelper.CopyImage(bmp);
        }
        catch { /* ignore */ }
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not InboxItemVM vm) return;
        try
        {
            if (File.Exists(vm.ImagePath))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{vm.ImagePath}\"") { UseShellExecute = true });
        }
        catch { /* ignore */ }
    }

    private void OnDeleteSelected(object sender, RoutedEventArgs e)
    {
        var sel = _all.Where(vm => vm.Selected).ToList();
        if (sel.Count == 0)
        {
            MessageWindow.Alert(this, Strings.Inbox_NoneSelected, Strings.Inbox_DeleteTitle, DialogKind.Info);
            return;
        }
        bool ok = MessageWindow.Confirm(this,
            string.Format(Strings.Inbox_DeleteSelectedConfirmFormat, sel.Count),
            Strings.Inbox_DeleteTitle, danger: true, okText: Strings.Common_Delete);
        if (!ok) return;

        foreach (var vm in sel)
        {
            CaptureIO.DeleteFiles(vm.ImagePath, _settings.Current.SidecarEnabled);
            _captures.Remove(vm.Record);
        }
        _captures.Save();
        ReloadList();
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not InboxItemVM vm) return;
        bool ok = MessageWindow.Confirm(this,
            string.Format(Strings.Inbox_DeleteConfirmFormat, vm.FileName),
            Strings.Inbox_DeleteTitle, danger: true, okText: Strings.Common_Delete);
        if (!ok) return;

        CaptureIO.DeleteFiles(vm.ImagePath, _settings.Current.SidecarEnabled);
        _captures.Remove(vm.Record);
        _captures.Save();
        ReloadList();
    }
}
