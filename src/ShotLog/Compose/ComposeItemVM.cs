using System;
using System.ComponentModel;
using System.Windows.Media;
using ShotLog.Infrastructure;
using ShotLog.Models;
using ShotLog.Resources;

namespace ShotLog.Compose;

/// <summary>A selectable capture in the compose list. Memo edits persist straight back to the store (+ sidecar).</summary>
public sealed class ComposeItemVM : INotifyPropertyChanged
{
    private readonly CaptureStore? _store;
    private readonly SettingsStore? _settings;
    private bool _selected = true;

    public ComposeItemVM(CaptureRecord r, CaptureStore? store = null, SettingsStore? settings = null)
    {
        Record = r;
        _store = store;
        _settings = settings;
        Thumb = ImageHelper.LoadThumb(r.ImagePath, 120);
    }

    public CaptureRecord Record { get; }
    public ImageSource? Thumb { get; }
    public string TimeText => Record.CapturedAt.ToString("HH:mm:ss");
    public string DateText => Record.CapturedAt.ToString("yyyy-MM-dd");

    /// <summary>Inline-editable memo; commits to the store and asks the window to refresh the preview.</summary>
    public string Memo
    {
        get => Record.Memo;
        set
        {
            if (Record.Memo == value) return;
            Record.Memo = value ?? "";
            if (_settings?.Current.SidecarEnabled == true) CaptureIO.WriteSidecar(Record);
            _store?.Save();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Memo)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MemoSnippet)));
            MemoChanged?.Invoke();
        }
    }

    /// <summary>Raised after a memo edit commits (so the window re-renders the preview).</summary>
    public event Action? MemoChanged;

    public string MemoSnippet
    {
        get
        {
            var m = Record.Memo?.Trim();
            if (string.IsNullOrEmpty(m)) return Strings.Compose_NoMemo;
            int nl = m.IndexOfAny(new[] { '\r', '\n' });
            if (nl >= 0) m = m[..nl];
            return m.Length > 60 ? m[..60] + "…" : m;
        }
    }

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            SelectionChanged?.Invoke();
        }
    }

    /// <summary>Raised when this row's checkbox toggles (so the window can refresh the preview).</summary>
    public event Action? SelectionChanged;

    public event PropertyChangedEventHandler? PropertyChanged;
}
