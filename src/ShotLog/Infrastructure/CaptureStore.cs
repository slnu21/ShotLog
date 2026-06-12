using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShotLog.Models;

namespace ShotLog.Infrastructure;

/// <summary>
/// The authoritative index of captures (image path + memo + tags), stored as
/// %APPDATA%\ShotLog\captures.json. Records are reference types: edit one in place, then Save().
/// </summary>
public sealed class CaptureStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShotLog");

    public static string FilePath => Path.Combine(Dir, "captures.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public List<CaptureRecord> Items { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                Items = JsonSerializer.Deserialize<List<CaptureRecord>>(json, JsonOptions) ?? new();
            }
            else
            {
                Items = new();
            }
        }
        catch
        {
            Items = new();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Items, JsonOptions));
        }
        catch
        {
            // best-effort
        }
    }

    public void Add(CaptureRecord record) => Items.Add(record);
    public void Remove(CaptureRecord record) => Items.Remove(record);
}
