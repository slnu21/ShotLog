using System;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace ShotLog.Infrastructure;

/// <summary>Bridges System.Drawing bitmaps and on-disk PNGs into WPF <see cref="BitmapSource"/>s.</summary>
public static class ImageHelper
{
    /// <summary>Snapshots a GDI bitmap into a frozen, self-contained WPF image (safe to use after the bitmap is disposed).</summary>
    public static BitmapSource ToBitmapSource(System.Drawing.Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.StreamSource = ms;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    /// <summary>Encodes a PNG on disk as a base64 <c>data:</c> URI, downscaled to <paramref name="maxWidth"/>
    /// so a rendered preview can embed it inline (no file:// access, no NavigateToString origin issues). Null on failure.</summary>
    public static string? ToDataUri(string path, int maxWidth = 900)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            using var src = new System.Drawing.Bitmap(path);
            System.Drawing.Bitmap? scaled = null;
            try
            {
                System.Drawing.Bitmap toEncode = src;
                if (src.Width > maxWidth)
                {
                    int w = maxWidth;
                    int h = Math.Max(1, (int)Math.Round(src.Height * (maxWidth / (double)src.Width)));
                    scaled = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = System.Drawing.Graphics.FromImage(scaled))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(src, 0, 0, w, h);
                    }
                    toEncode = scaled;
                }
                using var ms = new MemoryStream();
                toEncode.Save(ms, ImageFormat.Png);
                return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
            }
            finally { scaled?.Dispose(); }
        }
        catch { return null; }
    }

    /// <summary>Copies <paramref name="srcPath"/> to <paramref name="destPath"/> as PNG, downscaled to
    /// <paramref name="maxWidth"/> px wide. When <paramref name="maxWidth"/> &lt;= 0 or the source is already
    /// narrower, the file is copied verbatim (never upscales).</summary>
    public static void SaveResizedPng(string srcPath, string destPath, int maxWidth)
    {
        using var src = new System.Drawing.Bitmap(srcPath);
        if (maxWidth <= 0 || src.Width <= maxWidth)
        {
            File.Copy(srcPath, destPath, overwrite: false);
            return;
        }

        int w = maxWidth;
        int h = Math.Max(1, (int)Math.Round(src.Height * (maxWidth / (double)src.Width)));
        using var scaled = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(scaled))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, w, h);
        }
        scaled.Save(destPath, ImageFormat.Png);
    }

    /// <summary>Loads a downscaled thumbnail from a PNG path without keeping the file locked. Null on failure.</summary>
    public static BitmapImage? LoadThumb(string path, int decodeWidth = 260)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;          // read fully, then release the file
            bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bi.DecodePixelWidth = decodeWidth;
            bi.UriSource = new Uri(path);
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch
        {
            return null;
        }
    }
}
