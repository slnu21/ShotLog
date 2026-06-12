using System;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using ShotLog.Models;
using ShotLog.Resources;

namespace ShotLog.Infrastructure;

/// <summary>Writes capture PNGs and their optional same-named .md sidecars to disk.</summary>
public static class CaptureIO
{
    /// <summary>Saves <paramref name="bmp"/> as a timestamped PNG in <paramref name="folder"/>; returns the full path.</summary>
    public static string SavePng(System.Drawing.Bitmap bmp, string folder, DateTimeOffset at)
    {
        Directory.CreateDirectory(folder);
        string baseName = at.ToString("yyyy-MM-dd_HH-mm-ss");
        string path = Path.Combine(folder, baseName + ".png");
        int i = 2;
        while (File.Exists(path))
        {
            path = Path.Combine(folder, $"{baseName}_{i}.png");
            i++;
        }
        bmp.Save(path, ImageFormat.Png);
        return path;
    }

    /// <summary>(Re)writes a same-named .md next to the PNG so the memo is readable in Explorer too.</summary>
    public static void WriteSidecar(CaptureRecord r)
    {
        try
        {
            string side = Path.ChangeExtension(r.ImagePath, ".md");
            var sb = new StringBuilder();
            sb.AppendLine($"# {Path.GetFileNameWithoutExtension(r.ImagePath)}");
            sb.AppendLine();
            sb.AppendLine($"- {Strings.Sidecar_Time}: {r.CapturedAt:yyyy-MM-dd HH:mm:ss}");
            if (!string.IsNullOrWhiteSpace(r.PresetName)) sb.AppendLine($"- {Strings.Common_Preset}: {r.PresetName}");
            if (r.Tags.Count > 0) sb.AppendLine($"- {Strings.Common_Tags}: {string.Join(", ", r.Tags)}");
            sb.AppendLine();
            sb.AppendLine($"![]({Path.GetFileName(r.ImagePath)})");
            if (!string.IsNullOrWhiteSpace(r.Memo))
            {
                sb.AppendLine();
                sb.AppendLine(r.Memo.Trim());
            }
            File.WriteAllText(side, sb.ToString(), new UTF8Encoding(false));
        }
        catch { /* best-effort */ }
    }

    public static void DeleteFiles(string imagePath, bool sidecar)
    {
        try { if (File.Exists(imagePath)) File.Delete(imagePath); } catch { }
        if (!sidecar) return;
        try
        {
            var s = Path.ChangeExtension(imagePath, ".md");
            if (File.Exists(s)) File.Delete(s);
        }
        catch { }
    }
}
