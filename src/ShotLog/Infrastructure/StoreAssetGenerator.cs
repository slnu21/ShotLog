using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace ShotLog.Infrastructure;

/// <summary>
/// Generates the MSIX visual assets (logos / tiles / splash) and Store promo art from the shared
/// <see cref="AppIconFactory"/> mark. Run via the App's <c>--genassets</c> dev hook. Tiles are drawn
/// on a transparent canvas; the package manifest's <c>BackgroundColor</c> (#0D1117 navy) supplies the
/// plate behind the white note card, so one transparent asset works for both Start tiles and taskbar.
/// </summary>
public static class StoreAssetGenerator
{
    private static readonly int[] Scales = { 100, 125, 150, 200, 400 };
    private static readonly int[] TargetSizes = { 16, 24, 32, 48, 256 };

    /// <summary>Writes MSIX logos into <paramref name="imagesDir"/> and Store promo art into <paramref name="promoDir"/>.</summary>
    public static void Generate(string imagesDir, string promoDir)
    {
        Directory.CreateDirectory(imagesDir);
        Directory.CreateDirectory(promoDir);

        // Scaled logos. fraction = mark size as a share of the shorter canvas side.
        EmitScaled(imagesDir, "Square44x44Logo", 44, 44, 0.72);
        EmitScaled(imagesDir, "Square71x71Logo", 71, 71, 0.62);
        EmitScaled(imagesDir, "Square150x150Logo", 150, 150, 0.52);
        EmitScaled(imagesDir, "Square310x310Logo", 310, 310, 0.50);
        EmitScaled(imagesDir, "Wide310x150Logo", 310, 150, 0.62);
        EmitScaled(imagesDir, "StoreLogo", 50, 50, 0.72);
        EmitScaled(imagesDir, "SplashScreen", 620, 300, 0.42);

        // Taskbar / app-list target sizes for the 44x44 logo (plated + unplated; both transparent).
        foreach (int n in TargetSizes)
        {
            using var img = Compose(n, n, 0.86);
            Save(img, Path.Combine(imagesDir, $"Square44x44Logo.targetsize-{n}.png"));
            Save(img, Path.Combine(imagesDir, $"Square44x44Logo.targetsize-{n}_altform-unplated.png"));
        }

        // Store listing promo art (Partner Center).
        using (var tile = Compose(300, 300, 0.52)) Save(tile, Path.Combine(promoDir, "StoreTile-300x300.png"));
        using (var hero = BuildHero(1920, 1080)) Save(hero, Path.Combine(promoDir, "Hero-1920x1080.png"));
    }

    private static void EmitScaled(string dir, string name, int w, int h, double fraction)
    {
        foreach (int scale in Scales)
        {
            // MSIX validates each scaled asset at round-half-up(base * scale/100). Integer division
            // truncates the .5/.75 cases (e.g. 71×125% = 88.75 must be 89, not 88) and fails APPX1619.
            int sw = (int)Math.Round(w * scale / 100.0, MidpointRounding.AwayFromZero);
            int sh = (int)Math.Round(h * scale / 100.0, MidpointRounding.AwayFromZero);
            using var img = Compose(sw, sh, fraction);
            Save(img, Path.Combine(dir, $"{name}.scale-{scale}.png"));
            if (scale == 100) Save(img, Path.Combine(dir, $"{name}.png")); // unqualified base = scale-100
        }
    }

    /// <summary>The mark centered on a transparent canvas, sized to <paramref name="fraction"/> of the shorter side.</summary>
    private static Bitmap Compose(int w, int h, double fraction)
    {
        var canvas = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(canvas);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        int mark = Math.Max(1, (int)Math.Round(Math.Min(w, h) * fraction));
        using var icon = AppIconFactory.Render(mark);
        g.DrawImage(icon, (w - mark) / 2, (h - mark) / 2, mark, mark);
        return canvas;
    }

    private static Bitmap BuildHero(int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        using (var bg = new LinearGradientBrush(new Point(0, 0), new Point(w, h),
                   Color.FromArgb(0xFF, 0x0D, 0x11, 0x17), Color.FromArgb(0xFF, 0x16, 0x1B, 0x22)))
            g.FillRectangle(bg, 0, 0, w, h);

        int mark = (int)(h * 0.5);
        using (var icon = AppIconFactory.Render(mark))
            g.DrawImage(icon, (int)(w * 0.18) - mark / 2, (h - mark) / 2, mark, mark);

        using var title = new Font("Malgun Gothic", h * 0.10f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var sub = new Font("Malgun Gothic", h * 0.040f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var white = new SolidBrush(Color.FromArgb(0xFF, 0xE6, 0xED, 0xF3));
        using var muted = new SolidBrush(Color.FromArgb(0xFF, 0x8B, 0x94, 0x9E));
        float tx = w * 0.30f;
        g.DrawString("ShotLog", title, white, tx, h * 0.34f);
        g.DrawString("캡처하고, 메모하고, 한 번에 정리", sub, muted, tx + 4, h * 0.50f);
        return bmp;
    }

    private static void Save(Bitmap b, string path) => b.Save(path, ImageFormat.Png);
}
