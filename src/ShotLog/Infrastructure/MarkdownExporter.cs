using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ShotLog.Models;
using ShotLog.Resources;

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

    /// <summary>Writes a single self-contained <c>.html</c> file (images inlined as data URIs) to
    /// <paramref name="outputRoot"/> and returns its path. Reuses <see cref="BuildHtmlPreview"/>.</summary>
    public static string ExportHtml(IEnumerable<CaptureRecord> records, string title, string outputRoot,
        bool includeFrontMatter, Func<string, string?> imageToDataUri)
    {
        var list = records.OrderBy(r => r.CapturedAt).ToList();
        var date = list.Count > 0 ? list[0].CapturedAt : DateTimeOffset.Now;

        string slug = Slugify(title);
        if (string.IsNullOrWhiteSpace(slug)) slug = date.ToString("yyyy-MM-dd_HH-mm-ss");

        Directory.CreateDirectory(outputRoot);
        string path = Path.Combine(outputRoot, slug + ".html");
        int k = 2;
        while (File.Exists(path)) { path = Path.Combine(outputRoot, $"{slug}_{k}.html"); k++; }

        string html = BuildHtmlPreview(list, title, includeFrontMatter, imageToDataUri);
        File.WriteAllText(path, html, new UTF8Encoding(false));
        return path;
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

    /// <summary>
    /// Renders the same document as <see cref="BuildPreview"/> but as a self-contained HTML page (GitHub-dark
    /// styling) for the live rendered-preview pane. Images are resolved to inline data URIs via
    /// <paramref name="imageToDataUri"/> (keyed by the source path) so nothing is copied to disk.
    /// </summary>
    public static string BuildHtmlPreview(IEnumerable<CaptureRecord> records, string title, bool includeFrontMatter,
        Func<string, string?> imageToDataUri)
    {
        var list = records.OrderBy(r => r.CapturedAt).ToList();
        var date = list.Count > 0 ? list[0].CapturedAt : DateTimeOffset.Now;
        var allTags = list.SelectMany(r => r.Tags).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var body = new StringBuilder();

        if (includeFrontMatter)
        {
            var fm = new StringBuilder();
            fm.AppendLine("---");
            fm.AppendLine($"title: \"{title.Replace("\"", "'")}\"");
            fm.AppendLine($"date: {date:yyyy-MM-dd HH:mm:ss}");
            if (allTags.Count > 0) fm.AppendLine($"tags: [{string.Join(", ", allTags)}]");
            fm.Append("---");
            body.Append($"<pre class=\"frontmatter\">{Html(fm.ToString())}</pre>");
        }

        body.Append($"<h1>{Html(string.IsNullOrWhiteSpace(title) ? Strings.Export_Untitled : title)}</h1>");
        string meta = date.ToString("yyyy-MM-dd");
        if (allTags.Count > 0) meta += $" · {Strings.Common_Tags}: {string.Join(", ", allTags)}";
        body.Append($"<p class=\"meta\"><em>{Html(meta)}</em></p>");

        foreach (var r in list)
        {
            body.Append($"<h2>{Html(r.CapturedAt.ToString("HH:mm"))}</h2>");
            if (!string.IsNullOrWhiteSpace(r.Memo))
                body.Append($"<p>{Br(Html(r.Memo.Trim()))}</p>");
            if (!string.IsNullOrEmpty(r.ImagePath))
            {
                string alt = Html(AltFor(r, Path.GetFileName(r.ImagePath)));
                string? uri = imageToDataUri(r.ImagePath);
                body.Append(uri != null
                    ? $"<p><img alt=\"{alt}\" src=\"{uri}\"></p>"
                    : $"<p class=\"missing\">🖼 {alt}</p>");
            }
        }

        return HtmlShell(body.ToString());
    }

    private static string Html(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string Br(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "<br>");

    private static string HtmlShell(string body) =>
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>" +
        ":root{color-scheme:dark;}" +
        "html,body{margin:0;}" +
        "body{background:#0d1117;color:#e6edf3;font-family:'Pretendard','Segoe UI','Malgun Gothic',sans-serif;font-size:14px;line-height:1.6;padding:18px 22px;}" +
        "h1{font-size:1.7em;font-weight:700;border-bottom:1px solid #30363d;padding-bottom:.3em;margin:.1em 0 .6em;}" +
        "h2{font-size:1.3em;font-weight:600;border-bottom:1px solid #21262d;padding-bottom:.3em;margin:1.4em 0 .6em;}" +
        "p{margin:.6em 0;}" +
        ".meta{margin-top:-.3em;}em{color:#8b949e;}" +
        "img{max-width:100%;height:auto;border:1px solid #30363d;border-radius:8px;background:#0b0f15;}" +
        ".missing{color:#6e7681;}" +
        ".frontmatter{background:#161b22;border:1px solid #30363d;border-radius:8px;padding:10px 12px;color:#8b949e;font-family:Consolas,monospace;font-size:12px;white-space:pre-wrap;}" +
        "::-webkit-scrollbar{width:12px;height:12px;}" +
        "::-webkit-scrollbar-thumb{background:#30363d;border-radius:6px;border:3px solid #0d1117;}" +
        "</style></head><body>" + body + "</body></html>";

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

        sb.AppendLine($"# {(string.IsNullOrWhiteSpace(title) ? Strings.Export_Untitled : title)}");
        sb.AppendLine();
        string meta = $"_{date:yyyy-MM-dd}";
        if (allTags.Count > 0) meta += $" · {Strings.Common_Tags}: {string.Join(", ", allTags)}";
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
