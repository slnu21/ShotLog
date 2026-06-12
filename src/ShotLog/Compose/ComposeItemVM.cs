using System;
using System.ComponentModel;
using System.Windows.Media;
using ShotLog.Infrastructure;
using ShotLog.Models;

namespace ShotLog.Compose;

/// <summary>A selectable capture in the compose list.</summary>
public sealed class ComposeItemVM : INotifyPropertyChanged
{
    private bool _selected = true;

    public ComposeItemVM(CaptureRecord r)
    {
        Record = r;
        Thumb = ImageHelper.LoadThumb(r.ImagePath, 120);
    }

    public CaptureRecord Record { get; }
    public ImageSource? Thumb { get; }
    public string TimeText => Record.CapturedAt.ToString("HH:mm:ss");
    public string DateText => Record.CapturedAt.ToString("yyyy-MM-dd");

    public string MemoSnippet
    {
        get
        {
            var m = Record.Memo?.Trim();
            if (string.IsNullOrEmpty(m)) return "(메모 없음)";
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
