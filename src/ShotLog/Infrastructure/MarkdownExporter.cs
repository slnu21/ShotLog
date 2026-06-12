using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ShotLog.Models;

namespace ShotLog.Infrastructure;

/// <summary>
/// Builds a portable, standard-Markdown document from selected captures: a chronological list of
/// "## HH:mm → memo → ![](images/file.png)" blocks, with the images copied into a sibling images/ folder.
/// No platform-specific syntax (works in GitHub, VS Code, Obsidian, Typora, …).
/// </summary>
public static class MarkdownExporter
{
    public sealed record Result(string MarkdownPath, string FolderPath, int ImageCount);

    public static Result Export(IEnumerable<CaptureRecord> records, string title, string outputRoot, bool includeFrontMatter)
    {
        var list = records.OrderBy(r => r.CapturedAt).ToList();
        var date = list.Count > 0 ? list[0].CapturedAt : DateTimeOffset.Now;

        string slug = Slugify(title);
        if (string.IsNullOrWhiteSpace(slug))
            slug = date.ToString("yyyy-MM-dd_HH-mm-ss");

        string folder = Path.Combine(outputRoot, slug);
        string imagesDir = Path.Combine(folder, "images");
        Directory.CreateDirectory(imagesDir);

        var sb = new StringBuilder();
        WriteHeader(sb, list, title, includeFrontMatter);

        int imageCount = 0;
        foreach (var r in list)
        {
            WriteRecordHeader(sb, r);
            if (File.Exists(r.ImagePath))
            {
                string destName = UniqueDestName(imagesDir, r.ImagePath);
                try
                {
                    File.Copy(r.ImagePath, Path.Combine(imagesDir, destName), overwrite: false);
                    imageCount++;
                }
                catch { /* skip unreadable image, keep going */ }

                sb.AppendLine($"![{EscapeAlt(AltFor(r, destName))}](images/{destName})");
                sb.AppendLine();
            }
        }

        string mdPath = Path.Combine(folder, slug + ".md");
        File.WriteAllText(mdPath, sb.ToString(), new UTF8Encoding(false));
        return new Result(mdPath, folder, imageCount);
    }

    /// <summary>Same text Export would write, without copying any files — for the live preview pane.</summary>
    public static string BuildPreview(IEnumerable<CaptureRecord> records, string title, bool includeFrontMatter)
    {
        var list = records.OrderBy(r => r.CapturedAt).ToList();
        var sb = new StringBuilder();
        WriteHeader(sb, list, title, includeFrontMatter);
        foreach (var r in list)
        {
            WriteRecordHeader(sb, r);
            if (!string.IsNullOrEmpty(r.ImagePath))
            {
                string name = Path.GetFileName(r.ImagePath);
                sb.AppendLine($"![{EscapeAlt(AltFor(r, name))}](images/{name})");
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    private static void WriteHeader(StringBuilder sb, List<CaptureRecord> list, string title, bool includeFrontMatter)
    {
        var date = list.Count > 0 ? list[0].CapturedAt : DateTimeOffset.Now;
        var allTags = list.SelectMany(r => r.Tags).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (includeFrontMatter)
        {
            sb.AppendLine("---");
            sb.AppendLine($"title: \"{title.Replace("\"", "'")}\"");
            sb.AppendLine($"date: {date:yyyy-MM-dd HH:mm:ss}");
            if (allTags.Count > 0) sb.AppendLine($"tags: [{string.Join(", ", allTags)}]");
            sb.AppendLine("---");
            sb.AppendLine();
        }

        sb.AppendLine($"# {(string.IsNullOrWhiteSpace(title) ? "제목 없음" : title)}");
        sb.AppendLine();
        string meta = $"_{date:yyyy-MM-dd}";
        if (allTags.Count > 0) meta += $" · 태그: {string.Join(", ", allTags)}";
        meta += "_";
        sb.AppendLine(meta);
        sb.AppendLine();
    }

    private static void WriteRecordHeader(StringBuilder sb, CaptureRecord r)
    {
        sb.AppendLine($"## {r.CapturedAt:HH:mm}");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(r.Memo))
        {
            sb.AppendLine(r.Memo.Trim());
            sb.AppendLine();
        }
    }

    private static string AltFor(CaptureRecord r, string destName)
        => string.IsNullOrWhiteSpace(r.Memo) ? Path.GetFileNameWithoutExtension(destName) : FirstLine(r.Memo);

    private static string UniqueDestName(string imagesDir, string sourcePath)
    {
        string name = Path.GetFileName(sourcePath);
        string dest = Path.Combine(imagesDir, name);
        int k = 2;
        while (File.Exists(dest))
        {
            name = $"{Path.GetFileNameWithoutExtension(sourcePath)}_{k}{Path.GetExtension(sourcePath)}";
            dest = Path.Combine(imagesDir, name);
            k++;
        }
        return name;
    }

    private static string FirstLine(string s)
    {
        int nl = s.IndexOfAny(new[] { '\r', '\n' });
        return (nl < 0 ? s : s[..nl]).Trim();
    }

    private static string EscapeAlt(string s) => s.Replace("[", "(").Replace("]", ")");

    /// <summary>Filename-safe slug. Keeps Korean/Unicode letters and digits; spaces → hyphens; strips path-invalid chars.</summary>
    private static string Slugify(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(title.Length);
        foreach (char c in title.Trim())
        {
            if (char.IsWhiteSpace(c)) sb.Append('-');
            else if (Array.IndexOf(invalid, c) >= 0) { /* drop */ }
            else sb.Append(c);
        }
        var slug = sb.ToString();
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }
}
