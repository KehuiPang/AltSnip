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
            .LogToTrace();
}
