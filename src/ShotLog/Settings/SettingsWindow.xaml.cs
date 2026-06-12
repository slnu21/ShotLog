using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ShotLog.Infrastructure;
using ShotLog.Models;
using ShotLog.Resources;
using WinForms = System.Windows.Forms;

namespace ShotLog.Settings;

/// <summary>Edits presets, hotkeys, export options and autostart, then persists and re-applies them.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly ObservableCollection<PresetEditVM> _presets = new();

    /// <summary>Raised after a successful save (App re-registers hotkeys / applies autostart).</summary>
    public event Action? Saved;

    public SettingsWindow(SettingsStore settings)
    {
        InitializeComponent();
        _settings = settings;
        WindowChrome.ApplyDarkTitleBar(this);
        Load();
    }

    private void Load()
    {
        var s = _settings.Current;
        foreach (var p in s.Presets) _presets.Add(PresetEditVM.From(p));
        PresetList.ItemsSource = _presets;

        InstantBox.Text = s.InstantHotkey;
        NoteBox.Text = s.NoteHotkey;
        RegionBox.Text = s.RegionHotkey;
        WindowBox.Text = s.WindowHotkey;
        InboxBox.Text = s.InboxHotkey;

        ExportRootBox.Text = string.IsNullOrWhiteSpace(s.ExportRoot) ? App.ExportRoot() : s.ExportRoot;
        SidecarCheck.IsChecked = s.SidecarEnabled;
        NotifyCheck.IsChecked = s.NotifyOnCapture;

        // Reflect the real OS state (a packaged StartupTask can be turned off in Task Manager).
        AutoStartCheck.IsChecked = AutoStartService.IsEnabled();
        if (AutoStartService.IsLockedByUserOrPolicy())
        {
            AutoStartCheck.IsEnabled = false;
            AutoStartCheck.ToolTip = Strings.Settings_AutoStartLockedTip;
        }

        LanguageBox.SelectedIndex = s.Language switch { "ko" => 1, "en" => 2, _ => 0 };
    }

    private void OnAddPreset(object sender, RoutedEventArgs e)
        => _presets.Add(new PresetEditVM { Color = "#5AA0FF" });

    private void OnRemovePreset(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is PresetEditVM vm) _presets.Remove(vm);
    }

    private void OnBrowsePreset(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not PresetEditVM vm) return;
        using var dlg = new WinForms.FolderBrowserDialog();
        if (!string.IsNullOrWhiteSpace(vm.FolderPath)) dlg.SelectedPath = vm.FolderPath;
        if (dlg.ShowDialog() == WinForms.DialogResult.OK) vm.FolderPath = dlg.SelectedPath;
    }

    private void OnBrowseExport(object sender, RoutedEventArgs e)
    {
        using var dlg = new WinForms.FolderBrowserDialog();
        if (!string.IsNullOrWhiteSpace(ExportRootBox.Text)) dlg.SelectedPath = ExportRootBox.Text;
        if (dlg.ShowDialog() == WinForms.DialogResult.OK) ExportRootBox.Text = dlg.SelectedPath;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        // Validate hotkeys before committing anything.
        var gestures = new (string Label, string Value)[]
        {
            (Strings.Settings_HkShortInstant, InstantBox.Text),
            (Strings.Settings_HkShortNote, NoteBox.Text),
            (Strings.Settings_HkShortRegion, RegionBox.Text),
            (Strings.Settings_HkShortWindow, WindowBox.Text),
            (Strings.Settings_HkShortInbox, InboxBox.Text),
        };
        var invalid = gestures.Where(g => !HotkeyManager.TryParse(g.Value, out _, out _)).Select(g => g.Label).ToList();
        if (invalid.Count > 0)
        {
            MessageBox.Show(this, string.Format(Strings.Settings_InvalidHotkeyFormat, string.Join(", ", invalid)),
                "ShotLog", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Presets (drop empty rows; fill a default folder for name-only rows).
        var models = _presets.Where(p => !p.IsEmpty).Select(p => p.ToModel()).ToList();
        foreach (var m in models)
        {
            if (string.IsNullOrWhiteSpace(m.FolderPath))
            {
                string safe = Sanitize(string.IsNullOrWhiteSpace(m.Name) ? "preset" : m.Name);
                m.FolderPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ShotLog", safe);
            }
            if (string.IsNullOrWhiteSpace(m.Name)) m.Name = Path.GetFileName(m.FolderPath);
        }
        if (models.Count == 0)
        {
            models.Add(new Preset
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = Strings.Common_DefaultPreset,
                FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ShotLog"),
                Color = "#5AA0FF",
            });
        }

        var s = _settings.Current;
        s.Presets = models;
        if (models.All(p => p.Id != s.ActivePresetId)) s.ActivePresetId = models[0].Id;

        s.InstantHotkey = InstantBox.Text.Trim();
        s.NoteHotkey = NoteBox.Text.Trim();
        s.RegionHotkey = RegionBox.Text.Trim();
        s.WindowHotkey = WindowBox.Text.Trim();
        s.InboxHotkey = InboxBox.Text.Trim();

        s.ExportRoot = ExportRootBox.Text.Trim();
        s.SidecarEnabled = SidecarCheck.IsChecked == true;
        s.NotifyOnCapture = NotifyCheck.IsChecked == true;
        s.AutoStart = AutoStartCheck.IsChecked == true;
        s.Language = LanguageBox.SelectedIndex switch { 1 => "ko", 2 => "en", _ => "system" };

        _settings.Save();
        Saved?.Invoke();
        Close();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().Where(c => Array.IndexOf(invalid, c) < 0).ToArray();
        var s = new string(chars).Trim();
        return string.IsNullOrEmpty(s) ? "preset" : s;
    }
}
