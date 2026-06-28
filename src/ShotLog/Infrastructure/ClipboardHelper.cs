using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ShotLog.Infrastructure;

/// <summary>Copies an image to the Windows clipboard in both standard bitmap and PNG formats
/// (PNG preserves transparency for apps that prefer it). All failures are swallowed.</summary>
public static class ClipboardHelper
{
    public static bool CopyImage(System.Drawing.Bitmap bmp)
    {
        try { return CopyImage(ImageHelper.ToBitmapSource(bmp)); }
        catch { return false; }
    }

    public static bool CopyImage(BitmapSource img)
    {
        try
        {
            var data = new DataObject();
            data.SetImage(img);
            using (var ms = new MemoryStream())
            {
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(img));
                enc.Save(ms);
                data.SetData("PNG", ms);
                Clipboard.SetDataObject(data, true);   // copy=true flushes synchronously
            }
            return true;
        }
        catch { return false; }
    }
}
