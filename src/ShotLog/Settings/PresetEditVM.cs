using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ShotLog.Models;

namespace ShotLog.Settings;

/// <summary>Editable wrapper around a <see cref="Preset"/> for the settings list (tags as a comma string).</summary>
public sealed class PresetEditVM : INotifyPropertyChanged
{
    private string _name = "";
    private string _folderPath = "";
    private string _tagsText = "";
    private string _color = "#5AA0FF";

    public string Id { get; set; } = "";

    public string Name { get => _name; set { _name = value; OnChanged(nameof(Name)); } }
    public string FolderPath { get => _folderPath; set { _folderPath = value; OnChanged(nameof(FolderPath)); } }
    public string TagsText { get => _tagsText; set { _tagsText = value; OnChanged(nameof(TagsText)); } }
    public string Color { get => _color; set { _color = value; OnChanged(nameof(Color)); } }

    public static PresetEditVM From(Preset p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        FolderPath = p.FolderPath,
        TagsText = string.Join(", ", p.DefaultTags),
        Color = p.Color,
    };

    public Preset ToModel() => new()
    {
        Id = string.IsNullOrEmpty(Id) ? Guid.NewGuid().ToString("N") : Id,
        Name = (Name ?? "").Trim(),
        FolderPath = (FolderPath ?? "").Trim(),
        DefaultTags = (TagsText ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList(),
        Color = string.IsNullOrWhiteSpace(Color) ? "#5AA0FF" : Color.Trim(),
    };

    public bool IsEmpty => string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(FolderPath);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
