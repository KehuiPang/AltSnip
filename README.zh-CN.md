<p align="right"><a href="README.md">English</a> · <b>中文</b></p>

<p align="center">
  <img src="logo_256.png" width="104" alt="无为截">
</p>

<h1 align="center">无为截</h1>

<p align="center">
  <b>按 <code>Alt&nbsp;+&nbsp;A</code>,拖个框,截图就进了剪贴板。</b><br>
  一个快速、省心的截图 + 标注工具,支持 <b>Windows、macOS、Linux</b>。
</p>

<p align="center">
  <img src="https://img.shields.io/github/v/release/KehuiPang/wuwei-shot?color=f4b740" alt="release">
  <img src="https://img.shields.io/badge/platform-Windows%20%C2%B7%20macOS%20%C2%B7%20Linux-0078D6" alt="platform">
  <img src="https://img.shields.io/badge/license-MIT-2ebe6e" alt="license">
  <img src="https://img.shields.io/badge/runtime-self--contained-e0762a" alt="self-contained">
</p>

<p align="center">
  <img src="docs/demo.png" width="720" alt="无为截 演示 — 框选、箭头、文字、马赛克、复制">
</p>

<p align="center">
  <sub>↑ 动图为 APNG,现代浏览器均可播放</sub>
</p>

---

## 为什么做它

某天微信卡死,我想截个图都截不了,一怒之下花一个下午写了第一版。发现一个真正好用的截图工具也没多少代码,于是它长成了现在这样:一个热键,截图、标注、复制或保存,免费给所有人用,覆盖三大桌面系统。

- ⚡ **秒起** —— 全局热键 `Alt + A`,在哪都能唤起,不用找、不用点菜单。
- 🎯 **松手前一气呵成** —— 移动/缩放选框、标注、打码,然后复制或保存。
- 📦 **无需安装** —— 每个系统一个自包含文件,不装运行时、零依赖、无需配置。

## 功能

- **一个热键** —— `Alt + A` 冻结并压暗屏幕,选区保持清晰,实时显示像素尺寸 + 金色准星。
- **调整选框** —— 框内拖动整体移动,拖 8 个控制点缩放,框外拖动重新框选。
- **标注** —— 箭头、直线、方框、文字、马赛克,无边框极简工具条。
- **颜色和粗细** —— 7 种预设颜色 + 3 档线宽,一点即换。
- **马赛克打码** —— 框住手机号、人脸、密钥一拖即打码,分享前遮好。
- **文字(支持输入法)** —— 点一下直接打字,背景透明,中文照常。
- **复制或保存** —— ✓(或 `Enter`)复制到剪贴板,保存按钮导出 PNG。
- **多种取消** —— ✗、`Esc`、右键,或再按一次 `Alt + A`。
- **多显示器** —— 截取鼠标所在的那块屏,工具条始终留在可见屏幕内。
- **托盘常驻** —— 点托盘图标截图,右键退出。

## 下载

从 [最新发布](../../releases/latest) 下载对应系统的文件:

| 系统 | 下载 | 怎么运行 |
| --- | --- | --- |
| **Windows**(10/11) | `wuwei-shot-Windows-x64.exe` | 双击,待在托盘,按 `Alt + A`。 |
| **macOS**(Apple 芯片) | `wuwei-shot-macOS-arm64.zip` | 解压 → 右键应用 ▸ **打开**(未签名),授予屏幕录制权限。 |
| **macOS**(Intel) | `wuwei-shot-macOS-x64.zip` | 同上。 |
| **Linux** | `wuwei-shot-Linux-x64.AppImage` | `chmod +x` 后运行,需要 `grim`/`scrot` + `wl-copy`/`xclip`。 |

> [!NOTE]
> Windows 版已充分测试。**macOS / Linux 仍在真机验证中**,欢迎反馈。全局 `Alt + A` 在 Windows 与 X11 Linux 可用;macOS 暂用托盘菜单触发(系统级热键待补)。

> **想要极轻的纯 Windows 版?** 最初那个单文件 **约 50 KB** 的 `.exe`(WinForms,零依赖)在 [v1.4.3](../../releases/tag/v1.4.3)。

想开机自启:把它加进各系统的启动项即可。

> 提醒:微信截图默认快捷键也是 `Alt + A`。无为截 用底层键盘钩子拦截,谁先抢注都无效,不用改设置。

## 快捷键

| 按键 / 操作 | 作用 |
| --- | --- |
| `Alt + A` | 开始截图(已打开则取消) |
| 拖动 | 框选区域 |
| 框内拖动 / 控制点 | 移动 / 缩放选框 |
| `Enter` 或 ✓ | 复制到剪贴板 |
| 保存按钮 | 导出 PNG |
| `Ctrl + Z` / 撤销 | 删除上一笔标注 |
| `Esc` · 右键 · ✗ | 取消 |

## 两个版本

- **`cross/`** —— 跨平台应用(Avalonia + SkiaSharp),以 `v2.x` 发布,支持 Windows / macOS / Linux。这是主项目。
- **`src/Snip.cs`** —— 最初的纯 Windows 工具:单文件约 50 KB 的 WinForms `.exe`,零依赖(`v1.x` 发布)。只需要 Windows、又想要最小体积的话仍然很香。

## 从源码编译

**跨平台**(需要 .NET 8 SDK):

```bash
dotnet run --project cross/WuweiShot.Desktop.csproj
```

**经典 Windows 版**(不用 Visual Studio,Windows 自带 C# 编译器):

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

图标由 `tools/IconGen.cs` 生成,演示动画由 `tools/DemoGen.cs` 生成。`.github/workflows/` 里的 CI 会自动编译并打包三个平台。

## 工作原理

按下热键时,截取鼠标所在屏幕为位图。无边框遮罩把这张冻结图压暗显示,选区内以原亮度画回;标注画在上层,确认时烧进最终图。因为背景是冻结的,你的任何操作都不打扰底层程序。只有"截屏、全局热键、图片剪贴板"这三块是各系统各写,其余(界面、标注、马赛克、保存)都是共享代码。

## 许可协议

[MIT](LICENSE) —— 随便用。
