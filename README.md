<p align="center">
  <img src="logo_256.png" width="112" alt="AltSnip logo">
</p>

<h1 align="center">AltSnip</h1>

A tiny, no-dependency screenshot tool for Windows. Press **Alt + A**, drag a box, click the green check — your screenshot is on the clipboard, ready to paste anywhere.

No installer, no runtime download, no bloat. It's a single ~12 KB `.exe` that sits quietly in your tray. Built with the .NET Framework that already ships with Windows, so there's nothing else to install.

> I made this in an afternoon because WeChat froze while I was trying to grab a screenshot and I got fed up. Turns out a good snipping tool is about 400 lines of C#. Sharing it in case it saves someone else the same annoyance.

## Features

- **One hotkey** — `Alt + A` from anywhere brings up the capture overlay.
- **Freeze-frame selection** — the screen freezes and dims; your selection stays bright, with a live pixel-size readout.
- **Adjust the box** — drag inside to move it, or grab any of the 8 handles to resize. Drag outside to start over.
- **Annotate before you copy** — arrow, line, rectangle, text, and mosaic (blur) tools, in a clean borderless toolbar. Type a note next to your arrow (full IME support). `Undo` (or `Ctrl + Z`) removes the last mark.
- **Pick color & thickness** — 7 preset colors and 3 line widths appear when a drawing tool is active.
- **Mosaic** — drag over anything sensitive to pixelate it before sharing.
- **Copy or save** — ✓ (or `Enter`) copies to the clipboard; the save button writes a PNG wherever you choose.
- **Cancel any way** — ✗, `Esc`, right-click, or press `Alt + A` again.
- **Straight to clipboard** — paste into WeChat, chat apps, docs, image editors, anywhere with `Ctrl + V`.
- **Multi-monitor aware** — works across all your displays, including negative-coordinate layouts.
- **Tray resident** — double-click the tray icon to snip, right-click to quit.

## Download & Run

1. Grab `Snip.exe` from the [Releases](../../releases) page.
2. Double-click it. That's it — it goes to your system tray.
3. Press `Alt + A` and start snipping.

To launch it automatically at startup, drop a shortcut to `Snip.exe` into your Startup folder:

```
Win + R  →  shell:startup  →  paste a shortcut to Snip.exe
```

## Build from source

You don't need Visual Studio. Windows already has the C# compiler.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Or call the compiler directly:

```powershell
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /target:winexe /out:Snip.exe /reference:System.Drawing.dll /reference:System.Windows.Forms.dll src\Snip.cs
```

## How it works

On the hotkey, the whole virtual screen (all monitors) is copied into a bitmap. An overlay window shows that frozen bitmap dimmed; your drag rectangle redraws the bright, original pixels on top. On confirm, that region is cropped from the bitmap and pushed to the clipboard via `Clipboard.SetImage`. No screen re-capture after selection, so what you see is exactly what you get.

## License

[MIT](LICENSE) — do whatever you want with it.

---

# AltSnip（中文说明）

一个极简、零依赖的 Windows 截图小工具。按 **Alt + A**,拖个框,点绿色对勾 —— 截图就进了剪贴板,随处 `Ctrl + V` 粘贴。

没有安装包,不用下载运行时,不臃肿。就是一个约 12 KB 的单文件 `.exe`,安静地待在托盘里。用的是 Windows 自带的 .NET Framework 编译,不用额外装任何东西。

> 某天微信卡死、我想截个图都截不了,一怒之下自己写了这个。发现一个好用的截图工具也就 400 行 C#。开源出来,免得别人也被同样的事烦到。

## 功能

- **一个热键** —— 任何地方按 `Alt + A` 唤起截图遮罩。
- **冻结取景** —— 屏幕冻结变暗,选区保持清晰,实时显示像素尺寸。
- **调整选框** —— 框内拖动整体移动,拖 8 个控制点缩放,框外拖动重新框选;截歪了不用重来。
- **复制前先标注** —— 箭头、直线、方框、文字、马赛克五种工具,随手"指一下"再打字说明(支持中文输入法);无边框极简工具条,`撤销`(或 `Ctrl + Z`)删掉上一笔。
- **选颜色和粗细** —— 选中绘制工具时,下方出现 7 种预设颜色 + 3 档线条粗细。
- **马赛克打码** —— 框住敏感信息一拖即打码,分享前遮好。
- **复制或保存** —— ✓(或 `Enter`)复制到剪贴板;保存按钮导出 PNG 到任意位置。
- **多种取消** —— ✗、`Esc`、右键、或再按一次 `Alt + A`。
- **直达剪贴板** —— 微信、聊天软件、文档、图片编辑器,`Ctrl + V` 直接粘。
- **多显示器支持** —— 覆盖所有屏幕,含负坐标布局。
- **托盘常驻** —— 双击托盘图标截图,右键退出。

## 下载使用

1. 从 [Releases](../../releases) 页面下载 `Snip.exe`。
2. 双击运行,它会进入系统托盘。
3. 按 `Alt + A` 开始截图。

想开机自启:把 `Snip.exe` 的快捷方式放进启动文件夹 —— `Win + R` 输入 `shell:startup`,把快捷方式拖进去即可。

## 从源码编译

不需要 Visual Studio,Windows 自带 C# 编译器:

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

## 许可协议

[MIT](LICENSE) —— 随便用。
