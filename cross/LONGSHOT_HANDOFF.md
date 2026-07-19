# 长截图（微信式手动滚）交接文档 — 小码接手

> 董事长 2026-07-16 指定小码接手长截图这块原生窗口/像素级工作。小笨(CEO)已做完设计定稿 + 诊断 + 大部分实现，剩临门几脚。**品牌先行**：VI 用玄墨黑#16191E/月白#F4F6F8/竹青#5C8A73(主操作色)/一点朱#C05F3C(≤10%)。

## 目标交互（董事长逐条拍板，已定稿，勿改设计）
微信式**手动滚**：遮罩关掉后用户自己滚真 App，后台定时抓选区+增量拼接。
- 选区外**暗色半透明蒙层**压暗、选区清晰。
- 右侧**单框实时预览**（只长图、无文字/计数/面板）。
- **✓/✕ 矩形小按钮**（非圆形）钉在选区**右下角外侧**（框内会被截进图）。✓=完成并复制到剪贴板，✕/Esc=取消。
- 结果窗=**纯预览框**：无标题栏/无尺寸/无按钮，只有长图；Esc 关，右键另存/Ctrl+S。
- 草图基准：`/home/dacheng/.openclaw/workspace/projects/wuwei-shot-frame-final.png` 及 `wuwei-shot-longshot-v4.png`。

## 已实现（cross/ Avalonia net8.0，董事长实际在用那版）
- `Stitcher.cs` → `Accumulator`：增量累加+尾段匹配+一次合成，已去 O(n²)（治了旧版卡死的一半）。
- `LongShot.cs` → `LongSession` + `DimWindow`/`BorderWindow`/`PreviewWindow`/`ControlWindow`/`ResultWindow`。
- `Platform/WinOverlay.cs`：置顶/NOACTIVATE/点击穿透/工具窗 扩展样式。
- `Platform/WinScroll.cs`：`FocusTarget()` 用 AttachThreadInput 把目标窗口顶到前台取焦点。
- `OverlayControl`/`OverlayWindow`：长截图按钮(id=10)触发 `LongShot.Run(region, frame0, scaling, screenBounds)`。

## ⚠️ 待你收尾的（按优先级）
1. **【董事长刚点名的 bug】蒙层偏移**：`DimWindow` 选区外压暗，**顶部和左侧有空白没盖住**（"和上次一样"，是复发的坐标 bug）。查 `DimWindow` 的 `Position`/`Width`/`Height` 与 4 块矩形(上/下/左/右)的 hx/hy/fw/fh 计算——多屏/DPI/坐标原点(screen.Position vs 0,0)对齐问题。目标：蒙层严丝合缝盖满目标屏、只留选区一个洞。
2. **滚动真正跑通 + 预览实时长**：机制已验通（隔离测试 SetForegroundWindow+原生滚轮能滚 notepad），但**应用内完整流程 CEO 没能在董事长乱窗口桌面自测干净**。你确认：长截图启动后 `FocusTarget(target)` 是否真让目标窗口拿到焦点、用户滚轮是否驱动 `LongSession.Loop` 的抓取→`Accumulator.Feed`→`UpdatePreview`。Chromium/网页(class `Chrome_WidgetWin_1`)是主场景，务必拿浏览器长网页实测（PostMessage 顶层无效、必须走聚焦+原生滚轮，别走回头路，详见记忆 [[wuwei-shot-longshot]]）。
3. **✓/✕/边框是否被截进结果图**：控件在选区外、border 描边在选区外 2-3px，理论不被截，实测确认。
4. **"一开始有点卡"**（董事长反馈）：查 `Loop` 首个 `Task.Delay(320)` + 首帧抓取/预览合成节流是否造成初始顿挫。
5. mac/linux 目前只保证能编译；手动滚交互放最后。

## 构建 & 自测（董事长 Windows，WSL interop 已通，见记忆 [[windows-build-via-wsl-interop]]）
- .NET8 SDK：`C:\Users\Administrator\.dotnet\dotnet.exe`
- 编译：`dotnet build cross\WuweiShot.Desktop.csproj -c Release`
- 出可测自包含包：`dotnet publish ... -r win-x64 --self-contained true -o E:\3.git\wuwei-shot\test-build`（**改前先 `Stop-Process WuweiShot` 否则 exe 被锁、publish 不覆盖**）
- 触发截图免热键：`WuweiShot.exe --test-capture`
- 能截屏自测（`System.Windows.Forms` CopyFromScreen）。⚠️董事长桌面窗口多，自测选区易被别的 Chromium 窗口干扰，最好先 `Shell.Application.MinimizeAll()` 只留一个干净目标窗。
- **未 commit**：改顺了、董事长实测认可再 commit（同换色流程）。

## 规矩
- CEO(小笨)把关质量+盯进度+对董事长播报；你产出交回 CEO 过一道。完成后群里"✅ 已完成+成果位置"。对董事长只用中文。
