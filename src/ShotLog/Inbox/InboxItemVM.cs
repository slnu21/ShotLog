using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Media;
using ShotLog.Infrastructure;
using ShotLog.Models;

namespace ShotLog.Inbox;

/// <summary>Editable row for one capture. Memo/tag edits persist straight back to the store (+ sidecar).</summary>
public sealed class InboxItemVM : INotifyPropertyChanged
{
    private readonly CaptureRecord _r;
    private readonly CaptureStore _store;
    private readonly SettingsStore _settings;

    public InboxItemVM(CaptureRecord r, CaptureStore store, SettingsStore settings)
    {
        _r = r;
        _store = store;
        _settings = settings;
        Thumb = ImageHelper.LoadThumb(r.ImagePath, 200);
    }

    public CaptureRecord Record => _r;

    private bool _selected;
    /// <summary>Checkbox state for multi-select delete.</summary>
    public bool Selected
    {
        get => _selected;
        set { if (_selected == value) return; _selected = value; OnChanged(nameof(Selected)); }
    }

    public ImageSource? Thumb { get; }
    public bool ThumbMissing => Thumb == null;
    public string ImagePath => _r.ImagePath;
    public string FileName => Path.GetFileName(_r.ImagePath);
    public string TimeText => _r.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss");
    public string PresetName => string.IsNullOrEmpty(_r.PresetName) ? "—" : _r.PresetName;

    public string Memo
    {
        get => _r.Memo;
        set
        {
            if (_r.Memo == value) return;
            _r.Memo = value ?? "";
            Persist();
            OnChanged(nameof(Memo));
            OnChanged(nameof(HasMemo));
        }
    }

    public bool HasMemo => !string.IsNullOrWhiteSpace(_r.Memo);

    public string TagsText
    {
        get => string.Join(", ", _r.Tags);
        set
        {
            _r.Tags = Split(value);
            Persist();
            OnChanged(nameof(TagsText));
            OnChanged(nameof(TagList));
            OnChanged(nameof(HasTags));
        }
    }

    /// <summary>Tags as a chip-bindable list (read-only display).</summary>
    public IReadOnlyList<string> TagList => _r.Tags;
    public bool HasTags => _r.Tags.Count > 0;

    private bool _editingTags;
    /// <summary>Toggles the card's tag area between chips (false) and an inline editor (true).</summary>
    public bool EditingTags
    {
        get => _editingTags;
        set { if (_editingTags == value) return; _editingTags = value; OnChanged(nameof(EditingTags)); }
    }

    private static List<string> Split(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new();
        return s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    private void Persist()
    {
        if (_settings.Current.SidecarEnabled) CaptureIO.WriteSidecar(_r);
        _store.Save();
    }

    public bool Matches(string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return true;
        q = q.Trim();
        return _r.Memo.Contains(q, StringComparison.OrdinalIgnoreCase)
            || _r.PresetName.Contains(q, StringComparison.OrdinalIgnoreCase)
            || FileName.Contains(q, StringComparison.OrdinalIgnoreCase)
            || _r.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
