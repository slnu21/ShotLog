using System.Collections.Generic;
using System.IO;

namespace ShotLog.Infrastructure;

/// <summary>
/// Packs PNG frames into a single multi-resolution Windows .ico. Uses PNG-compressed entries
/// (supported by Windows Vista+), so no BMP/AND-mask encoding is needed. No external dependency.
/// </summary>
public static class IcoWriter
{
    /// <summary>Writes <paramref name="frames"/> (each an already-PNG-encoded square image) as an .ico at <paramref name="path"/>.</summary>
    public static void Write(string path, IReadOnlyList<(int Size, byte[] Png)> frames)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs);

        // ICONDIR
        w.Write((ushort)0); // reserved
        w.Write((ushort)1); // type = icon
        w.Write((ushort)frames.Count);

        // ICONDIRENTRY[] — 16 bytes each; image data follows the directory.
        int offset = 6 + 16 * frames.Count;
        foreach (var f in frames)
        {
            w.Write((byte)(f.Size >= 256 ? 0 : f.Size)); // width  (0 ⇒ 256)
            w.Write((byte)(f.Size >= 256 ? 0 : f.Size)); // height (0 ⇒ 256)
            w.Write((byte)0);            // palette colors (0 = true-color)
            w.Write((byte)0);            // reserved
            w.Write((ushort)1);          // color planes
            w.Write((ushort)32);         // bits per pixel
            w.Write((uint)f.Png.Length); // bytes of image data
            w.Write((uint)offset);       // offset of image data
            offset += f.Png.Length;
        }

        foreach (var f in frames)
            w.Write(f.Png);
    }
}
