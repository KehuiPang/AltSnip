namespace AltSnip.Platform;

/// <summary>macOS 实现（截屏 screencapture/CGDisplay / 热键 Carbon 或 CGEventTap / NSPasteboard）——里程碑 1 起填充。</summary>
public sealed class MacServices : IPlatformServices
{
    public string Name => "macos";
}
