namespace AltSnip.Platform;

/// <summary>Windows 实现（截屏 BitBlt / 全局热键 RegisterHotKey / 图片剪贴板）——里程碑 1 起填充。</summary>
public sealed class WindowsServices : IPlatformServices
{
    public string Name => "windows";
}
