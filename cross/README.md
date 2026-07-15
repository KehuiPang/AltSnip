# Wuwei Shot — cross-platform (Windows / macOS / Linux)

A ground-up rewrite of Wuwei Shot on [Avalonia](https://avaloniaui.net/) so it runs on
**Windows, macOS, and Linux** from one codebase — shipped as the `v2.x` releases.
The original `../src/Snip.cs` is a Windows-only WinForms build kept around as an
ultra-light option (`v1.x`).

## Why a rewrite (not just "another build")

The classic app is built directly on Win32 — WinForms UI, `SetWindowsHookEx` for the
global hotkey, `user32`/`gdi32` for capture and clipboard. None of that exists on
macOS or Linux, so genuine cross-platform support means:

- **UI** → Avalonia (Skia-rendered, runs everywhere).
- **Global hotkey** → per-OS: Win32 `RegisterHotKey`, macOS Carbon/`CGEventTap`
  (needs Accessibility permission), Linux X11 `XGrabKey` / evdev.
- **Screen capture** → per-OS: Win32 BitBlt, macOS `CGDisplayCreateImage` /
  `screencapture`, Linux X11 `XGetImage` / `grim` (Wayland).
- **Image clipboard** → per-OS: Win32 clipboard, macOS `NSPasteboard`, Linux `xclip`.

These are hidden behind `Platform/IPlatformServices` so the shared UI/annotation layer
stays clean.

## Status — roadmap

- [x] **M0** — Avalonia skeleton + platform abstraction + 3-OS CI.
- [x] **M1** — capture → drag a selection → copy to clipboard. Windows via Win32
  (BitBlt / CF_DIB); macOS via `screencapture` + `osascript`; Linux via
  `grim`/`scrot` + `wl-copy`/`xclip`.
- [x] **M2** — tray icon triggers a capture on every OS. Global `Alt + A`:
  Windows (`WH_KEYBOARD_LL`) ✅, Linux (X11 `XGrabKey`) best-effort, macOS via
  tray (Carbon hotkey TODO).
- [x] **M3** — full annotation engine in SkiaSharp: arrow / line / rectangle /
  text (IME) / mosaic, color + thickness, move/resize handles, undo, copy, save PNG.
- [x] **M4** — packaged per OS (Windows `.exe`, macOS `.app` in a `.zip`, Linux
  `.AppImage`) by `scripts/package.sh`, attached to the
  [`v2.0.0`](../../releases/tag/v2.0.0) release.
- [ ] **Next** — real-hardware testing on macOS & Linux; code signing; capture across
  multiple monitors at once; macOS global hotkey (Carbon).

**Verified:** compiles on all platforms (CI); the **Windows build is fully working**
(capture, annotate, mosaic, copy, save, hidden cursor + gold crosshair). **Not yet
verified:** macOS & Linux runtime behaviour — help welcome. The maintainer's dev box
has no .NET SDK, so CI is the source of truth.

## Build

Requires the .NET 8 SDK.

```bash
dotnet run --project cross/WuweiShot.Desktop.csproj
# or publish a self-contained single file:
dotnet publish cross/WuweiShot.Desktop.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```
