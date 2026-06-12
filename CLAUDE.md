# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ShotLog is a tray-resident Windows screenshot + memo tool (.NET 8, WPF with WinForms interop, Korean UI). Global hotkeys capture the screen; each capture is saved as a timestamped PNG plus a record (memo + tags) in a JSON index. Captures can be reviewed in the Inbox and exported as a portable Markdown document. There is no main window — the app lives in the system tray.

## Build & run

```powershell
dotnet build ShotLog.sln                              # build
dotnet run --project src/ShotLog/ShotLog.csproj       # run (launches tray app)
```

Windows-only: targets `net8.0-windows10.0.19041.0` (runtime floor 17763). Uses both `UseWPF` and `UseWindowsForms` (WPF windows + a WinForms `NotifyIcon` tray). Open `ShotLog.sln` in Visual Studio 2022 to debug.

## Verification (no test framework)

There is no unit-test project. Verification is a headless self-test driven by a CLI flag:

```powershell
dotnet run --project src/ShotLog/ShotLog.csproj -- --selftest
```

This runs the full capture → save PNG → write `.md` sidecar → export Markdown pipeline with no UI and no mutation of the real stores, writing the result (including the generated Markdown) to `%APPDATA%\ShotLog\selftest.log`. See `RunSelfTestAndExit()` in `App.xaml.cs`. When changing the capture/save/export pipeline, run `--selftest` and check `RESULT=OK` in that log.

## Architecture

**`App.xaml.cs` is the orchestrator.** It is the tray-resident `Application` entry point. It owns two static stores (`App.Settings`, `App.Captures`), wires the tray menu and global hotkeys to capture flows, and manages the windows. Start here to understand any flow.

**Four capture flows**, each starting from a hotkey or tray item:
- **Instant** (`CaptureInstant`) — captures the cursor's monitor and saves straight to the active preset with no UI and no focus steal. All exceptions are swallowed (a hotkey must never crash the app).
- **Memo** (`CaptureMonitorWithNote`) — captures, then opens `QuickNoteWindow` to add memo/tags before saving.
- **Region** (`CaptureRegion`) — captures, opens `RegionSelectWindow` to crop, then `QuickNoteWindow`.
- **Window** (`CaptureActiveWindow`) — `PrintWindow` of the foreground window, then `QuickNoteWindow`.

**Data model** (`Models/`):
- `CaptureRecord` — the authoritative link between a PNG (`ImagePath`) and its note (`Memo`, `Tags`). `PresetName` is denormalized on purpose so a row still reads correctly if the preset is later renamed/deleted.
- `Preset` — a named save destination (folder + accent color + default tags), shown as a chip at capture time.
- `AppSettings` — presets, the five hotkey gesture strings, export root, and toggles (sidecar, notify, autostart).

**Persistence** — two JSON files under `%APPDATA%\ShotLog\`, each managed by a store with best-effort IO (every read/write is wrapped in try/catch and never throws):
- `settings.json` ↔ `SettingsStore` / `AppSettings`
- `captures.json` ↔ `CaptureStore` / `List<CaptureRecord>`

Records are reference types: mutate one in place, then call `Save()`. Beyond the JSON index there are two derived Markdown outputs:
- **Sidecar** (`CaptureIO.WriteSidecar`) — an optional same-named `.md` written next to each PNG so the memo is visible in Explorer.
- **Export** (`MarkdownExporter`) — bundles selected captures into a folder of standard Markdown plus a sibling `images/` copy, deliberately platform-neutral (renders in GitHub / VS Code / Obsidian). `BuildPreview` produces the same text without copying files, for the live preview pane.

**Capture backend is abstracted** behind `ICaptureService`. The only implementation is `GdiCaptureService` (GDI `CopyFromScreen` + `PrintWindow`). The interface exists so a `Windows.Graphics.Capture` backend (for exclusive-fullscreen games, which can come back black under GDI) can be slotted in later. `MonitorShot` carries pixel geometry + DPI scale so a region can be cropped from a full-monitor capture.

**Native interop** lives in `Infrastructure/Native.cs` (P/Invoke). `MonitorHelper` resolves the cursor's monitor geometry in **physical pixels for capture** and **DIPs for window placement** — keep that distinction when touching capture or window positioning. `HotkeyManager` registers global hotkeys against a message-only window and parses gesture strings like `"Ctrl+Alt+S"`.

**Lifecycle infrastructure** (`Infrastructure/`):
- `SingleInstance` — named-mutex single instance; a second launch signals the first to show the Inbox instead of starting a new process.
- `AutoStartService` — start-with-Windows via the HKCU `Run` key (ShotLog ships as a portable exe, not MSIX).
- `TrayIconService` — the `NotifyIcon` and its context menu, surfaced to `App` as events.

## UI conventions

- Feature folders, each with XAML windows: `Capture/`, `Compose/`, `Inbox/`, `Settings/`. Management windows (Inbox, Compose, Settings) are kept as a single live instance each by `App`.
- No MVVM framework. Windows are mostly code-behind; the few `*VM` classes (`ComposeItemVM`, `InboxItemVM`, `PresetEditVM`) are lightweight item view-models, not a full MVVM stack.
- **All user-facing strings are Korean.** Match that when adding UI text.

## Gotchas

- **Bitmap ownership.** Captured `System.Drawing.Bitmap`s must be disposed exactly once. Instant capture disposes in a `finally`; `QuickNoteWindow` and `RegionSelectWindow` take ownership and dispose on close. When adding a flow that creates or passes a bitmap, be explicit about who disposes it.
- **Never throw on a hotkey or on store IO** — the codebase deliberately swallows these; follow that pattern rather than adding throwing paths to capture handlers or `Load`/`Save`.
- Several infra files (`SettingsStore`, `HotkeyManager`, `AutoStartService`) were ported from a prior project ("OrbitDock") and note it in comments.
- `design/mockup.html` is a static HTML design reference, not part of the build.
