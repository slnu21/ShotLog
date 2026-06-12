using System;
using System.Collections.Generic;

namespace ShotLog.Models;

/// <summary>
/// One capture: the saved PNG plus its memo/tags. The authoritative link between an image and
/// its note — <see cref="ImagePath"/> identifies the file, the rest is the note recorded for it.
/// </summary>
public sealed class CaptureRecord
{
    public string Id { get; set; } = "";
    public DateTimeOffset CapturedAt { get; set; }
    public string ImagePath { get; set; } = "";
    public string PresetId { get; set; } = "";
    /// <summary>Denormalised so the row still reads correctly if the preset is later renamed/removed.</summary>
    public string PresetName { get; set; } = "";
    public string Memo { get; set; } = "";
    public List<string> Tags { get; set; } = new();
}
