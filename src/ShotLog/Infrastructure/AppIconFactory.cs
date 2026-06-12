using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ShotLog.Infrastructure;

/// <summary>
/// Draws the ShotLog mark on a transparent canvas: a white ruled note card with a photo
/// (sun + mountains, accent gradient) inset at the top — screenshot + memo, the app concept.
/// Single source of truth for the tray icon and the generated multi-size app .ico.
/// </summary>
public static class AppIconFactory
{
    // Palette — matches the App.xaml dark-theme tokens.
    private static readonly Color Accent     = Color.FromArgb(0xFF, 0x5A, 0xA0, 0xFF); // #5AA0FF
    private static readonly Color AccentDeep = Color.FromArgb(0xFF, 0x2F, 0x6F, 0xE0); // #2F6FE0
    private static readonly Color CardFill   = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF); // white note
    private static readonly Color CardBorder = Color.FromArgb(0xFF, 0xAE, 0xB8, 0xC4); // soft gray edge
    private static readonly Color RuleColor  = Color.FromArgb(0xFF, 0xC2, 0xCD, 0xDA); // ruled lines

    /// <summary>Renders the icon at <paramref name="size"/>×<paramref name="size"/> px (32bpp ARGB, transparent bg).</summary>
    public static Bitmap Render(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        float s = size;
        bool tiny = size <= 24;

        // --- White note card ---
        float margin = s * 0.085f;
        var card = new RectangleF(margin, margin, s - 2 * margin, s - 2 * margin);
        float cardRadius = s * 0.14f;
        using (var path = Rounded(card, cardRadius))
        {
            using var fill = new SolidBrush(CardFill);
            g.FillPath(fill, path);
            using var pen = new Pen(CardBorder, Math.Max(1f, s * 0.018f));
            g.DrawPath(pen, path);
        }

        // Inner content box.
        float pad = s * 0.10f;
        var inner = new RectangleF(card.Left + pad, card.Top + pad, card.Width - 2 * pad, card.Height - 2 * pad);

        // --- Photo box (top) with sun + mountains ---
        float photoH = inner.Height * (tiny ? 0.52f : 0.46f);
        var photo = new RectangleF(inner.Left, inner.Top, inner.Width, photoH);
        using (var ppath = Rounded(photo, s * 0.06f))
        {
            using (var grad = new LinearGradientBrush(
                new PointF(photo.Left, photo.Top), new PointF(photo.Left, photo.Bottom), Accent, AccentDeep))
                g.FillPath(grad, ppath);

            g.SetClip(ppath);

            using (var sun = new SolidBrush(Color.FromArgb(235, Color.White)))
            {
                float r = photo.Height * 0.18f;
                float cx = photo.Left + photo.Width * 0.30f;
                float cy = photo.Top + photo.Height * 0.34f;
                g.FillEllipse(sun, cx - r, cy - r, r * 2, r * 2);
            }

            using (var mtn = new SolidBrush(Color.FromArgb(245, Color.White)))
            {
                float baseY = photo.Bottom;
                using (var m1 = new GraphicsPath())
                {
                    m1.AddPolygon(new[]
                    {
                        new PointF(photo.Left + photo.Width * 0.14f, baseY),
                        new PointF(photo.Left + photo.Width * 0.45f, photo.Top + photo.Height * 0.48f),
                        new PointF(photo.Left + photo.Width * 0.74f, baseY),
                    });
                    g.FillPath(mtn, m1);
                }
                using (var m2 = new GraphicsPath())
                {
                    m2.AddPolygon(new[]
                    {
                        new PointF(photo.Left + photo.Width * 0.52f, baseY),
                        new PointF(photo.Left + photo.Width * 0.78f, photo.Top + photo.Height * 0.62f),
                        new PointF(photo.Left + photo.Width * 1.04f, baseY),
                    });
                    g.FillPath(mtn, m2);
                }
            }

            g.ResetClip();
        }

        // --- Ruled lines (bottom) ---
        float linesTop = photo.Bottom + inner.Height * 0.14f;
        float linesBottom = inner.Bottom;
        int lineCount = tiny ? 2 : 3;
        float thk = Math.Max(1f, s * 0.028f);
        using (var rpen = new Pen(RuleColor, thk) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            for (int i = 0; i < lineCount; i++)
            {
                float y = linesTop + (linesBottom - linesTop) * i / (lineCount - 1);
                // Last line runs short for a hand-written "note" feel.
                float right = inner.Right - (i == lineCount - 1 ? inner.Width * 0.28f : 0f);
                g.DrawLine(rpen, inner.Left, y, right, y);
            }
        }

        return bmp;
    }

    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
