using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ShotLog.Infrastructure;

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
        AddFilterChip("전체", null);
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
        EmptyHint.Text = _all.Count == 0
            ? "아직 캡처가 없습니다. 단축키로 캡처해 보세요."
            : "조건에 맞는 캡처가 없습니다.";
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

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not InboxItemVM vm) return;
        var r = MessageBox.Show(this,
            $"이 캡처를 삭제할까요?\n{vm.FileName}\n\n이미지 파일도 함께 삭제됩니다.",
            "ShotLog", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (r != MessageBoxResult.OK) return;

        CaptureIO.DeleteFiles(vm.ImagePath, _settings.Current.SidecarEnabled);
        _captures.Remove(vm.Record);
        _captures.Save();
        ReloadList();
    }
}
