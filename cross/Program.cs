using System;
using Avalonia;

namespace AltSnip;

internal static class Program
{
    // Avalonia 需要 STA + 在任何 Avalonia 代码前不要初始化其它 UI
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // macOS：不在 Dock 显示图标（后台代理型，只留菜单栏托盘图标）——
            // Avalonia 原生选项，可靠地把激活策略设为 Accessory；比启动后再 interop 无时序竞争
            .With(new MacOSPlatformOptions { ShowInDock = false })
            .LogToTrace();
}
