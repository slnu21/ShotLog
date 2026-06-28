using System;
using System.Collections.Generic;
using System.Globalization;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ShotLog.Capture;
using ShotLog.Compose;
using ShotLog.Inbox;
using ShotLog.Infrastructure;
using ShotLog.Models;
using ShotLog.Resources;
using ShotLog.Settings;

namespace ShotLog;

/// <summary>
/// Tray-resident entry point. Bootstraps the stores, tray and global hotkeys, then orchestrates
/// the capture flows (instant / +memo / region / active window) and the management windows.
/// </summary>
public partial class App : Application
{
    public static SettingsStore Settings { get; private set; } = null!;
    public static CaptureStore Captures { get; private set; } = null!;

    private SingleInstance? _single;
    private TrayIconService? _tray;
    private HotkeyManager? _hotkeys;
    private readonly ICaptureService _capture = new GdiCaptureService();

    private InboxWindow? _inbox;
    private ComposeWindow? _compose;
    private SettingsWindow? _settings;

    // UI language. The OS culture captured at load is the "system" baseline; ApplyCulture drives
    // resource lookup (Strings) and the thread UI culture for any new windows.
    private static readonly CultureInfo _osUICulture = CultureInfo.CurrentUICulture;
    private static string _currentLang = "";

    private static void ApplyCulture(string lang)
    {
        _currentLang = lang;
        CultureInfo c = lang switch
        {
            "ko" => new CultureInfo("ko-KR"),
            "en" => new CultureInfo("en-US"),
            _ => _osUICulture,                 // "system": follow the OS (neutral English fallback)
        };
        Strings.Culture = lang == "system" ? null : c;
        CultureInfo.CurrentUICulture = c;
        CultureInfo.DefaultThreadCurrentUICulture = c;
    }

    /// <summary>Live-applies a language change: re-cultures, rebuilds the tray menu, and drops open
    /// windows so they recreate (re-evaluating their x:Static strings) in the new language. Transient
    /// capture windows are created per use, so they pick up the new language automatically.</summary>
    private void ApplyLanguageChange()
    {
        ApplyCulture(Settings.Current.Language);
        _tray?.RebuildMenu();
        _inbox?.Close();
        _compose?.Close();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Headless verification of the capture → save → sidecar → Markdown pipeline (no UI, no store mutation).
        if (e.Args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            RunSelfTestAndExit();
            return;
        }

        // Dev hook: render the app mark to a multi-size shotlog.ico + review PNGs, then exit.
        // Usage: ShotLog.exe --genicon <outputDir>
        if (e.Args.Any(a => string.Equals(a, "--genicon", StringComparison.OrdinalIgnoreCase)))
        {
            RunGenIconAndExit(e.Args);
            return;
        }

        // Dev hook: render the MSIX visual assets + Store promo art, then exit.
        // Usage: ShotLog.exe --genassets <imagesDir> [promoDir]
        if (e.Args.Any(a => string.Equals(a, "--genassets", StringComparison.OrdinalIgnoreCase)))
        {
            RunGenAssetsAndExit(e.Args);
            return;
        }

        // Dev hook: seed sample captures, render the main windows to Store listing screenshots, then exit.
        // Usage: ShotLog.exe --screens <outputDir>
        if (e.Args.Any(a => string.Equals(a, "--screens", StringComparison.OrdinalIgnoreCase)))
        {
            RunScreensAndExit(e.Args);
            return;
        }

        _single = new SingleInstance("ShotLog.SingleInstance.4af19c3e");
        if (!_single.IsFirstInstance)
        {
            _single.SignalFirstInstance();
            Shutdown();
            return;
        }
        _single.SecondInstanceRequested += (_, __) => Dispatcher.Invoke(ShowInbox);

        Settings = new SettingsStore();
        Settings.Load();
        ApplyCulture(Settings.Current.Language);
        EnsureDefaults();

        Captures = new CaptureStore();
        Captures.Load();

        AutoStartService.Apply(Settings.Current.AutoStart);

        _tray = new TrayIconService();
        _tray.CaptureMonitorRequested += (_, __) => CaptureInstant();
        _tray.CaptureNoteRequested += (_, __) => CaptureMonitorWithNote();
        _tray.CaptureRegionRequested += (_, __) => CaptureRegion();
        _tray.CaptureWindowRequested += (_, __) => CaptureActiveWindow();
        _tray.CaptureClipboardRequested += (_, __) => CaptureInstantToClipboard();
        _tray.InboxRequested += (_, __) => ShowInbox();
        _tray.ComposeRequested += (_, __) => ShowCompose();
        _tray.SettingsRequested += (_, __) => ShowSettings();
        _tray.ExitRequested += (_, __) => Shutdown();
        _tray.Show();

        RegisterHotkeys();
    }

    private void RegisterHotkeys()
    {
        _hotkeys?.Dispose();
        _hotkeys = new HotkeyManager();
        var s = Settings.Current;
        _hotkeys.TryRegister(s.InstantHotkey, () => Dispatcher.Invoke(CaptureInstant));
        _hotkeys.TryRegister(s.NoteHotkey, () => Dispatcher.Invoke(CaptureMonitorWithNote));
        _hotkeys.TryRegister(s.RegionHotkey, () => Dispatcher.Invoke(CaptureRegion));
        _hotkeys.TryRegister(s.WindowHotkey, () => Dispatcher.Invoke(CaptureActiveWindow));
        _hotkeys.TryRegister(s.ClipboardHotkey, () => Dispatcher.Invoke(CaptureInstantToClipboard));
        _hotkeys.TryRegister(s.InboxHotkey, () => Dispatcher.Invoke(ShowInbox));
    }

    // ---- preset helpers ----

    private void EnsureDefaults()
    {
        var s = Settings.Current;
        if (s.Presets.Count == 0)
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ShotLog");
            s.Presets.Add(new Preset
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = Strings.Common_DefaultPreset,
                FolderPath = folder,
                Color = "#5AA0FF",
            });
        }
        if (string.IsNullOrEmpty(s.ActivePresetId) || s.Presets.All(p => p.Id != s.ActivePresetId))
            s.ActivePresetId = s.Presets[0].Id;
        Settings.Save();
    }

    public static Preset ActivePreset()
    {
        var s = Settings.Current;
        return s.Presets.FirstOrDefault(p => p.Id == s.ActivePresetId) ?? s.Presets[0];
    }

    public static string ExportRoot()
    {
        var r = Settings.Current.ExportRoot;
        if (!string.IsNullOrWhiteSpace(r)) return r;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ShotLog-export");
    }

    // ---- capture flows ----

    /// <summary>Hotkey → capture the active monitor and save straight to the active preset (no UI, no focus steal).</summary>
    private void CaptureInstant()
    {
        try
        {
            var shot = _capture.CaptureActiveMonitor();
            try
            {
                var preset = ActivePreset();
                var at = DateTimeOffset.Now;
                string path = CaptureIO.SavePng(shot.Image, preset.FolderPath, at);
                var rec = new CaptureRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    CapturedAt = at,
                    ImagePath = path,
                    PresetId = preset.Id,
                    PresetName = preset.Name,
                    Tags = new(preset.DefaultTags),
                };
                Captures.Add(rec);
                Captures.Save();
                if (Settings.Current.SidecarEnabled) CaptureIO.WriteSidecar(rec);
                if (Settings.Current.NotifyOnCapture)
                    _tray?.Notify("ShotLog", string.Format(Strings.Notify_SavedFormat, preset.Name, Path.GetFileName(path), Settings.Current.NoteHotkey));
                _inbox?.ReloadIfVisible();
            }
            finally
            {
                shot.Image.Dispose();
            }
        }
        catch { /* never crash on a hotkey */ }
    }

    /// <summary>Hotkey → capture the active monitor straight to the clipboard (no save, no UI).</summary>
    private void CaptureInstantToClipboard()
    {
        try
        {
            var shot = _capture.CaptureActiveMonitor();
            try
            {
                ClipboardHelper.CopyImage(shot.Image);
                if (Settings.Current.NotifyOnCapture)
                    _tray?.Notify("ShotLog", Strings.Notify_Clipboard);
            }
            finally { shot.Image.Dispose(); }
        }
        catch { /* never crash on a hotkey */ }
    }

    private void CaptureMonitorWithNote()
    {
        try
        {
            var shot = _capture.CaptureActiveMonitor();
            // QuickNote takes ownership of the bitmap (disposes it on close).
            OpenQuickNote(shot.Image);
        }
        catch { }
    }

    private void CaptureRegion()
    {
        try
        {
            var shot = _capture.CaptureActiveMonitor();
            try
            {
                var win = new RegionSelectWindow(shot);
                bool ok = win.ShowDialog() == true && win.Result != null;
                if (ok) OpenQuickNote(win.Result!);
            }
            finally
            {
                shot.Image.Dispose();
            }
        }
        catch { }
    }

    private void CaptureActiveWindow()
    {
        try
        {
            var bmp = _capture.CaptureActiveWindow();
            if (bmp != null) OpenQuickNote(bmp);
        }
        catch { }
    }

    private void OpenQuickNote(System.Drawing.Bitmap bmp)
    {
        var w = new QuickNoteWindow(bmp, Settings, Captures);
        w.Saved += () => _inbox?.ReloadIfVisible();
        w.Show();
        w.Activate();
    }

    // ---- management windows (single live instance each) ----

    private void ShowInbox()
    {
        if (_inbox == null)
        {
            _inbox = new InboxWindow(Settings, Captures);
            _inbox.ComposeRequested += ShowCompose;
            _inbox.Closed += (_, __) => _inbox = null;
        }
        _inbox.ReloadList();
        ShowWindow(_inbox);
    }

    private void ShowCompose()
    {
        if (_compose == null)
        {
            _compose = new ComposeWindow(Settings, Captures);
            _compose.Closed += (_, __) => _compose = null;
        }
        _compose.ReloadData();
        ShowWindow(_compose);
    }

    private void ShowSettings()
    {
        if (_settings == null)
        {
            _settings = new SettingsWindow(Settings);
            _settings.Saved += () =>
            {
                RegisterHotkeys();
                AutoStartService.Apply(Settings.Current.AutoStart);
                if (Settings.Current.Language != _currentLang) ApplyLanguageChange();
                _inbox?.ReloadIfVisible();
            };
            _settings.Closed += (_, __) => _settings = null;
        }
        ShowWindow(_settings);
    }

    private static void ShowWindow(Window w)
    {
        if (!w.IsVisible) w.Show();
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Activate();
        w.Topmost = true;
        w.Topmost = false;
    }

    private void RunSelfTestAndExit()
    {
        var sb = new StringBuilder();
        string logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShotLog");
        try
        {
            var cap = new GdiCaptureService();
            var shot = cap.CaptureActiveMonitor();
            sb.AppendLine($"monitor: {shot.PxWidth}x{shot.PxHeight} px @ scale {shot.Scale}");

            string tmp = Path.Combine(Path.GetTempPath(), "ShotLog-selftest");
            string shotsDir = Path.Combine(tmp, "shots");
            var at = DateTimeOffset.Now;
            string png = CaptureIO.SavePng(shot.Image, shotsDir, at);
            shot.Image.Dispose();
            sb.AppendLine($"png: {png} exists={File.Exists(png)} bytes={new FileInfo(png).Length}");

            var rec = new CaptureRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                CapturedAt = at,
                ImagePath = png,
                PresetId = "selftest",
                PresetName = "셀프테스트",
                Memo = "셀프테스트 메모\n둘째 줄",
                Tags = new() { "테스트", "캡처" },
            };
            CaptureIO.WriteSidecar(rec);
            string side = Path.ChangeExtension(png, ".md");
            sb.AppendLine($"sidecar: {side} exists={File.Exists(side)}");

            string outRoot = Path.Combine(tmp, "export");
            var res = MarkdownExporter.Export(new[] { rec }, "셀프 테스트 기록", outRoot, includeFrontMatter: false);
            sb.AppendLine($"markdown: {res.MarkdownPath} exists={File.Exists(res.MarkdownPath)} images={res.ImageCount}");
            sb.AppendLine($"hotkey parse Ctrl+Alt+S = {HotkeyManager.TryParse("Ctrl+Alt+S", out _, out _)}");
            sb.AppendLine("----- generated markdown -----");
            sb.AppendLine(File.ReadAllText(res.MarkdownPath));
            sb.AppendLine("RESULT=OK");
        }
        catch (Exception ex)
        {
            sb.AppendLine("RESULT=FAIL " + ex);
        }

        try
        {
            Directory.CreateDirectory(logDir);
            File.WriteAllText(Path.Combine(logDir, "selftest.log"), sb.ToString());
        }
        catch { /* best-effort */ }

        Shutdown();
    }

    /// <summary>Renders shotlog.ico (16…256 px) + review PNGs into the output dir, then exits.</summary>
    private void RunGenIconAndExit(string[] args)
    {
        int idx = Array.FindIndex(args, a => string.Equals(a, "--genicon", StringComparison.OrdinalIgnoreCase));
        string outDir = (idx >= 0 && idx + 1 < args.Length && !args[idx + 1].StartsWith("--"))
            ? args[idx + 1]
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShotLog", "icon-out");

        var sb = new StringBuilder();
        try
        {
            Directory.CreateDirectory(outDir);
            int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
            var frames = new List<(int, byte[])>();
            foreach (int sz in sizes)
            {
                using var bmp = AppIconFactory.Render(sz);
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                frames.Add((sz, ms.ToArray()));
            }

            string ico = Path.Combine(outDir, "shotlog.ico");
            IcoWriter.Write(ico, frames);
            sb.AppendLine($"ico: {ico} bytes={new FileInfo(ico).Length} frames={frames.Count}");

            using (var p256 = AppIconFactory.Render(256))
                p256.Save(Path.Combine(outDir, "preview-256.png"), System.Drawing.Imaging.ImageFormat.Png);
            SavePreviewStrip(Path.Combine(outDir, "preview-sizes.png"));
            sb.AppendLine("RESULT=OK outDir=" + outDir);
        }
        catch (Exception ex)
        {
            sb.AppendLine("RESULT=FAIL " + ex);
        }

        try
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShotLog");
            Directory.CreateDirectory(logDir);
            File.WriteAllText(Path.Combine(logDir, "genicon.log"), sb.ToString());
        }
        catch { /* best-effort */ }

        Shutdown();
    }

    /// <summary>Renders the MSIX logos/tiles/splash + Store promo art, then exits.</summary>
    private void RunGenAssetsAndExit(string[] args)
    {
        int idx = Array.FindIndex(args, a => string.Equals(a, "--genassets", StringComparison.OrdinalIgnoreCase));
        string imagesDir = (idx >= 0 && idx + 1 < args.Length && !args[idx + 1].StartsWith("--"))
            ? args[idx + 1]
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShotLog", "assets", "Images");
        string promoDir = (idx >= 0 && idx + 2 < args.Length && !args[idx + 2].StartsWith("--"))
            ? args[idx + 2]
            : Path.Combine(Path.GetDirectoryName(imagesDir.TrimEnd('\\', '/')) ?? imagesDir, "promo");

        var sb = new StringBuilder();
        try
        {
            StoreAssetGenerator.Generate(imagesDir, promoDir);
            int images = Directory.GetFiles(imagesDir, "*.png").Length;
            int promo = Directory.GetFiles(promoDir, "*.png").Length;
            sb.AppendLine($"images: {imagesDir} ({images} png)");
            sb.AppendLine($"promo:  {promoDir} ({promo} png)");
            sb.AppendLine("RESULT=OK");
        }
        catch (Exception ex)
        {
            sb.AppendLine("RESULT=FAIL " + ex);
        }

        try
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShotLog");
            Directory.CreateDirectory(logDir);
            File.WriteAllText(Path.Combine(logDir, "genassets.log"), sb.ToString());
        }
        catch { /* best-effort */ }

        Shutdown();
    }

    /// <summary>A side-by-side dark/light strip of the icon at several sizes, for visual review.</summary>
    private static void SavePreviewStrip(string path)
    {
        int[] sizes = { 16, 24, 32, 48, 64, 128 };
        const int pad = 18, gap = 18, band = 128;
        int w = pad;
        foreach (int s in sizes) w += s + gap;
        int h = band + pad * 2;

        using var bmp = new System.Drawing.Bitmap(w, h);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var dark = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0xFF, 0x0D, 0x11, 0x17)))
            g.FillRectangle(dark, 0, 0, w / 2, h);
        using (var light = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0xFF, 0xF2, 0xF4, 0xF7)))
            g.FillRectangle(light, w / 2, 0, w - w / 2, h);

        int x = pad;
        foreach (int s in sizes)
        {
            using var ic = AppIconFactory.Render(s);
            g.DrawImage(ic, x, pad + (band - s), s, s);
            x += s + gap;
        }
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    /// <summary>Seeds sample captures, renders the main windows onto 1366×768 canvases for the Store listing, then exits.</summary>
    private void RunScreensAndExit(string[] args)
    {
        int idx = Array.FindIndex(args, a => string.Equals(a, "--screens", StringComparison.OrdinalIgnoreCase));
        string outDir = (idx >= 0 && idx + 1 < args.Length && !args[idx + 1].StartsWith("--"))
            ? args[idx + 1]
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShotLog", "screens");
        // Optional language for localized screenshots: --screens <dir> [ko|en]. Default follows the OS.
        string lang = (idx >= 0 && idx + 2 < args.Length && (args[idx + 2] == "ko" || args[idx + 2] == "en"))
            ? args[idx + 2] : "system";
        ApplyCulture(lang);
        bool en = lang == "en";

        var sb = new StringBuilder();
        try
        {
            Directory.CreateDirectory(outDir);
            string sampleDir = Path.Combine(Path.GetTempPath(), "ShotLog-screens");
            Directory.CreateDirectory(sampleDir);

            // Seed the static stores the windows read from.
            Settings = new SettingsStore();
            Settings.Current.Presets = new()
            {
                new Models.Preset { Id = "p-work", Name = en ? "Work log" : "작업 로그", FolderPath = sampleDir, Color = "#5AA0FF", DefaultTags = new() { en ? "work" : "작업" } },
                new Models.Preset { Id = "p-bug",  Name = en ? "Bug" : "버그",           FolderPath = sampleDir, Color = "#F85149", DefaultTags = new() { en ? "bug" : "버그" } },
                new Models.Preset { Id = "p-idea", Name = en ? "Idea" : "아이디어",       FolderPath = sampleDir, Color = "#7DEFD6", DefaultTags = new() { en ? "idea" : "아이디어" } },
            };
            Settings.Current.ActivePresetId = "p-work";

            Captures = new CaptureStore();
            var seed = en
                ? new (string Label, System.Drawing.Color Tint, string PresetId, string PName, string Memo, string[] Tags)[]
                {
                    ("Dashboard load delay",  System.Drawing.Color.FromArgb(0x5A, 0xA0, 0xFF), "p-bug",  "Bug",      "First load takes 3s+. Suspect a cache miss.", new[] { "bug", "perf" }),
                    ("Checkout flow mockup",  System.Drawing.Color.FromArgb(0x2D, 0xD4, 0xBF), "p-work", "Work log", "Propose cutting 2 steps down to 1.",          new[] { "work", "UX" }),
                    ("Onboarding copy review",System.Drawing.Color.FromArgb(0xE3, 0xB3, 0x41), "p-idea", "Idea",     "Unify the tone of the welcome copy.",         new[] { "idea" }),
                    ("Settings screen layout",System.Drawing.Color.FromArgb(0xF8, 0x51, 0x49), "p-work", "Work log", "Scrollbar covered the button → fixed.",        new[] { "work", "bug" }),
                }
                : new (string Label, System.Drawing.Color Tint, string PresetId, string PName, string Memo, string[] Tags)[]
                {
                    ("대시보드 로딩 지연", System.Drawing.Color.FromArgb(0x5A, 0xA0, 0xFF), "p-bug",  "버그",     "첫 로딩이 3초+ 걸림. 캐시 미스 의심.", new[] { "버그", "성능" }),
                    ("결제 플로우 시안",  System.Drawing.Color.FromArgb(0x2D, 0xD4, 0xBF), "p-work", "작업 로그", "2단계 → 1단계로 단축 제안.",       new[] { "작업", "UX" }),
                    ("온보딩 카피 검토",  System.Drawing.Color.FromArgb(0xE3, 0xB3, 0x41), "p-idea", "아이디어", "환영 문구 톤 통일 필요.",           new[] { "아이디어" }),
                    ("설정 화면 정렬",    System.Drawing.Color.FromArgb(0xF8, 0x51, 0x49), "p-work", "작업 로그", "스크롤바가 버튼에 가림 → 수정함.",   new[] { "작업", "버그" }),
                };
            var now = DateTimeOffset.Now;
            for (int i = 0; i < seed.Length; i++)
            {
                string png = Path.Combine(sampleDir, $"sample-{i}.png");
                using (var s = MakeSampleBitmap(seed[i].Label, seed[i].Tint)) s.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                Captures.Add(new CaptureRecord
                {
                    Id = "s" + i, CapturedAt = now.AddMinutes(-12 * i), ImagePath = png,
                    PresetId = seed[i].PresetId, PresetName = seed[i].PName, Memo = seed[i].Memo, Tags = new(seed[i].Tags),
                });
            }

            var inbox = new InboxWindow(Settings, Captures);
            ShotWindow(inbox, () => inbox.ReloadList(), outDir, "01-inbox.png", sb);

            var compose = new ComposeWindow(Settings, Captures);
            ShotWindow(compose, () => compose.ReloadData(), outDir, "02-compose.png", sb);

            var settingsWin = new SettingsWindow(Settings);
            ShotWindow(settingsWin, null, outDir, "03-settings.png", sb);

            var quick = new QuickNoteWindow(MakeSampleBitmap(en ? "Checkout flow mockup" : "결제 플로우 시안", System.Drawing.Color.FromArgb(0x2D, 0xD4, 0xBF)), Settings, Captures);
            ShotWindow(quick, null, outDir, "04-quicknote.png", sb);

            sb.AppendLine("RESULT=OK outDir=" + outDir);
        }
        catch (Exception ex)
        {
            sb.AppendLine("RESULT=FAIL " + ex);
        }

        try
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShotLog");
            Directory.CreateDirectory(logDir);
            File.WriteAllText(Path.Combine(logDir, "screens.log"), sb.ToString());
        }
        catch { /* best-effort */ }

        Shutdown();
    }

    /// <summary>Shows a window off-screen, lets it lay out, renders it, and composites it onto a 1366×768 dark canvas.</summary>
    private static void ShotWindow(Window w, Action? afterShow, string outDir, string file, StringBuilder sb)
    {
        try
        {
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Left = -10000;
            w.Top = -10000;
            w.ShowInTaskbar = false;
            w.Show();
            afterShow?.Invoke();
            w.UpdateLayout();
            w.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
            w.UpdateLayout();

            int pw = Math.Max(1, (int)Math.Ceiling(w.ActualWidth));
            int ph = Math.Max(1, (int)Math.Ceiling(w.ActualHeight));
            var rtb = new RenderTargetBitmap(pw, ph, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(w);
            SaveComposited(rtb, Path.Combine(outDir, file));
            sb.AppendLine($"{file}: {pw}x{ph}");
            w.Close();
        }
        catch (Exception ex)
        {
            sb.AppendLine($"{file}: FAIL {ex.Message}");
        }
    }

    private static void SaveComposited(RenderTargetBitmap shot, string path)
    {
        const int cw = 1366, ch = 768;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0D, 0x11, 0x17)), null, new Rect(0, 0, cw, ch));
            double scale = Math.Min(1.0, Math.Min((cw - 48.0) / shot.PixelWidth, (ch - 48.0) / shot.PixelHeight));
            double dw = shot.PixelWidth * scale, dh = shot.PixelHeight * scale;
            dc.DrawImage(shot, new Rect((cw - dw) / 2, (ch - dh) / 2, dw, dh));
        }
        var outRtb = new RenderTargetBitmap(cw, ch, 96, 96, PixelFormats.Pbgra32);
        outRtb.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(outRtb));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    /// <summary>A synthetic "app window" image used to populate the sample captures in screenshots.</summary>
    private static System.Drawing.Bitmap MakeSampleBitmap(string label, System.Drawing.Color tint)
    {
        const int w = 1280, h = 800;
        var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var g = System.Drawing.Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using (var bg = new System.Drawing.Drawing2D.LinearGradientBrush(
                   new System.Drawing.Point(0, 0), new System.Drawing.Point(w, h),
                   System.Drawing.Color.FromArgb(0xFF, 0x12, 0x16, 0x1E),
                   System.Drawing.Color.FromArgb(0xFF, tint.R / 5 + 0x12, tint.G / 5 + 0x12, tint.B / 5 + 0x12)))
            g.FillRectangle(bg, 0, 0, w, h);

        using (var card = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0xFF, 0x1C, 0x23, 0x2C)))
            g.FillRectangle(card, 80, 90, w - 160, h - 180);
        using (var bar = new System.Drawing.SolidBrush(tint))
            g.FillRectangle(bar, 80, 90, w - 160, 56);
        using (var dot = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)))
        {
            g.FillEllipse(dot, 106, 110, 16, 16);
            g.FillEllipse(dot, 134, 110, 16, 16);
            g.FillEllipse(dot, 162, 110, 16, 16);
        }
        using (var title = new System.Drawing.Font("Malgun Gothic", 30, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel))
        using (var white = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0xFF, 0xE6, 0xED, 0xF3)))
            g.DrawString(label, title, white, 120, 188);
        using (var line = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0xFF, 0x30, 0x3A, 0x46)))
            for (int r = 0; r < 8; r++)
                g.FillRectangle(line, 120, 252 + r * 46, (w - 320) - (r % 3) * 120, 14);

        g.Dispose();
        return bmp;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        _tray?.Dispose();
        _single?.Dispose();
        base.OnExit(e);
    }
}
