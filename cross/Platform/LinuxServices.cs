namespace AltSnip.Platform;

/// <summary>Linux 实现（X11 XGetImage / grim(Wayland) / XGrabKey / xclip 图片剪贴板）——里程碑 1 起填充。</summary>
public sealed class LinuxServices : IPlatformServices
{
    public string Name => "linux";
}
