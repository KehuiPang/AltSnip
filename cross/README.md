# AltSnip — cross-platform (work in progress)

A ground-up rewrite of AltSnip on [Avalonia](https://avaloniaui.net/) so it can run on
**Windows, macOS, and Linux** from one codebase. The original `../src/Snip.cs` is
Windows-only (WinForms + Win32) and stays as the stable Windows build; this folder is
the portable future.

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

- [x] **M0** — Avalonia skeleton + platform abstraction + 3-OS CI that compiles &
  publishes (Windows / macOS arm64+x64 / Linux). *Built in the cloud via GitHub
  Actions; the maintainer's dev box has no .NET SDK, so CI is the source of truth.*
- [ ] **M1** — capture the screen → drag a selection → copy to clipboard (per-OS).
- [ ] **M2** — global `Alt + A` hotkey + tray icon, per-OS.
- [ ] **M3** — port annotations (arrow / line / rect / text / mosaic), colors, save PNG.
- [ ] **M4** — polish, packaging (`.app`/AppImage), signed releases for all three.

macOS and Linux behaviour needs testing on real machines — help welcome.

## Build

Requires the .NET 8 SDK.

```bash
dotnet run --project cross/AltSnip.Desktop.csproj
# or publish a self-contained single file:
dotnet publish cross/AltSnip.Desktop.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```
