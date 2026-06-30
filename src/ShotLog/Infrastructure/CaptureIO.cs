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

    /// <summary>Overwrites an existing PNG in place (used after annotating a saved capture).</summary>
    public static void OverwritePng(System.Drawing.Bitmap bmp, string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        bmp.Save(path, ImageFormat.Png);
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

    /// <summary>Moves a capture's PNG (and its .md sidecar, when <paramref name="sidecar"/>) into
    /// <paramref name="destFolder"/>, updating <see cref="CaptureRecord.ImagePath"/>. Best-effort: returns
    /// false (and leaves the record untouched) if the move fails. Never throws.</summary>
    public static bool MoveCapture(CaptureRecord r, string destFolder, bool sidecar)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(r.ImagePath) || !File.Exists(r.ImagePath)) return false;
            Directory.CreateDirectory(destFolder);

            string srcDir = Path.GetDirectoryName(r.ImagePath) ?? "";
            if (string.Equals(Path.GetFullPath(srcDir), Path.GetFullPath(destFolder), StringComparison.OrdinalIgnoreCase))
                return true;    // already in the destination folder — nothing to move

            string destPath = UniqueInFolder(destFolder, r.ImagePath);
            File.Move(r.ImagePath, destPath);

            if (sidecar)
            {
                string srcSide = Path.ChangeExtension(r.ImagePath, ".md");
                if (File.Exists(srcSide))
                {
                    try { File.Move(srcSide, Path.ChangeExtension(destPath, ".md")); } catch { /* best-effort */ }
                }
            }

            r.ImagePath = destPath;
            return true;
        }
        catch { return false; }
    }

    private static string UniqueInFolder(string folder, string sourcePath)
    {
        string name = Path.GetFileName(sourcePath);
        string dest = Path.Combine(folder, name);
        int k = 2;
        while (File.Exists(dest))
        {
            name = $"{Path.GetFileNameWithoutExtension(sourcePath)}_{k}{Path.GetExtension(sourcePath)}";
            dest = Path.Combine(folder, name);
            k++;
        }
        return dest;
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
