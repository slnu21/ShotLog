using System.Collections.Generic;

namespace ShotLog.Models;

/// <summary>A named save destination shown as a chip at capture time.</summary>
public sealed class Preset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public List<string> DefaultTags { get; set; } = new();
    /// <summary>Accent colour (#RRGGBB) for the preset's chip swatch.</summary>
    public string Color { get; set; } = "#5AA0FF";
}
