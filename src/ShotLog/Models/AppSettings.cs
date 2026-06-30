using System.Collections.Generic;

namespace ShotLog.Models;

/// <summary>Persisted user settings (presets, hotkeys, export options). Serialized to %APPDATA%\ShotLog\settings.json.</summary>
public sealed class AppSettings
{
    public List<Preset> Presets { get; set; } = new();

    /// <summary>Preset that instant-capture saves into (and the default selection in the memo popup).</summary>
    public string ActivePresetId { get; set; } = "";

    // Global hotkeys (gesture strings parsed by HotkeyManager).
    public string InstantHotkey { get; set; } = "Ctrl+Alt+S";
    public string NoteHotkey { get; set; } = "Ctrl+Alt+D";
    public string RegionHotkey { get; set; } = "Ctrl+Alt+R";
    public string WindowHotkey { get; set; } = "Ctrl+Alt+W";
    public string ClipboardHotkey { get; set; } = "Ctrl+Alt+C";
    public string InboxHotkey { get; set; } = "Ctrl+Alt+I";

    /// <summary>Root folder for "글쓰기 내보내기" output. Empty → Documents\ShotLog-export at use time.</summary>
    public string ExportRoot { get; set; } = "";

    /// <summary>Last-used export image width in px (downscale only). 0 → keep original size.</summary>
    public int ExportImageWidth { get; set; } = 0;

    /// <summary>Sticky default for the Inbox "move to preset" dialog: replace tags with the target preset's defaults.</summary>
    public bool MoveReplaceTags { get; set; } = false;

    /// <summary>Write a same-named .md next to each PNG so the memo is visible in Explorer too.</summary>
    public bool SidecarEnabled { get; set; } = true;

    /// <summary>Show a tray balloon after an instant capture.</summary>
    public bool NotifyOnCapture { get; set; } = true;

    public bool AutoStart { get; set; } = false;

    /// <summary>UI language: "system" (follow OS), "ko", or "en". Applied at startup by App.ApplyCulture.</summary>
    public string Language { get; set; } = "system";
}
