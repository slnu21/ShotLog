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
