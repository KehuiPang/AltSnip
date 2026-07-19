using System;
using WuweiShot.Platform;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace WuweiShot;

public partial class App : Application
{
    private Window? _host;          // 隐形宿主：提供 Screens 信息 + 保活
    private IDisposable? _hotkey;
    private bool _capturing;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _host = new Window
            {
                Width = 1,
                Height = 1,
                SystemDecorations = SystemDecorations.None,
                ShowInTaskbar = false,
                Topmost = false,
                Opacity = 0,
                Position = new PixelPoint(-32000, -32000),
            };
            _host.Show();

            SetupTray();
            try { _hotkey = PlatformServices.Current.RegisterHotkey(Capture); } catch { }

            // 隐藏自测：启动即触发一次截图，便于开发时验证覆盖层
            var cmd = Environment.GetCommandLineArgs();
            foreach (var a in cmd)
                if (a == "--test-capture")
                    Avalonia.Threading.Dispatcher.UIThread.Post(Capture,
                        Avalonia.Threading.DispatcherPriority.Background);

            // 隐藏自测：--selftest-longshot x y w h —— 用固定选区直接跑长截图会话(免手动拖选)，
            // 供开发时截图验证蒙层盖满/按钮亮显/预览首帧。坐标为物理像素，缺省居中 700x500。
            for (int i = 0; i < cmd.Length; i++)
                if (cmd[i] == "--selftest-longshot")
                {
                    int gx = i + 1 < cmd.Length && int.TryParse(cmd[i + 1], out var vx) ? vx : int.MinValue;
                    int gy = i + 2 < cmd.Length && int.TryParse(cmd[i + 2], out var vy) ? vy : 0;
                    int gw = i + 3 < cmd.Length && int.TryParse(cmd[i + 3], out var vw) ? vw : 700;
                    int gh = i + 4 < cmd.Length && int.TryParse(cmd[i + 4], out var vh) ? vh : 500;
                    Avalonia.Threading.Dispatcher.UIThread.Post(
                        () => SelfTestLongShot(gx, gy, gw, gh),
                        Avalonia.Threading.DispatcherPriority.Background);
                }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SelfTestLongShot(int gx, int gy, int gw, int gh)
    {
        try
        {
            var all = _host?.Screens.All;
            if (all == null || all.Count == 0) return;
            var cur = PlatformServices.Current.CursorPosition();
            var screen = (cur.HasValue ? _host!.Screens.ScreenFromPoint(cur.Value) : null)
                         ?? _host!.Screens.Primary ?? all[0];
            var b = screen.Bounds;
            // 缺省居中；给定坐标则相对该屏原点
            int x = gx == int.MinValue ? b.X + (b.Width - gw) / 2 : b.X + gx;
            int y = gx == int.MinValue ? b.Y + (b.Height - gh) / 2 : b.Y + gy;
            var region = new PixelRect(x, y, Math.Min(gw, b.Width), Math.Min(gh, b.Height));
            var frame0 = PlatformServices.Current.CaptureRegion(region);
            LongShot.Run(region, frame0, screen.Scaling, b);
        }
        catch { }
    }

    private static WindowIcon LoadIcon()
        => new WindowIcon(AssetLoader.Open(new Uri("avares://WuweiShot/Assets/logo.png")));

    private void SetupTray()
    {
        // macOS 全局热键 Option+A(=Alt+A)，用 CGEventTap 拦截、优先级高于微信；其它平台 Alt+A
        string hk = OperatingSystem.IsMacOS() ? "⌥A" : "Alt+A";
        var tray = new TrayIcon { ToolTipText = $"无为截 — {hk} to capture", Icon = LoadIcon() };
        tray.Clicked += (_, _) => Capture();

        var menu = new NativeMenu();
        var cap = new NativeMenuItem($"Capture ({hk})");
        cap.Click += (_, _) => Capture();
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => Shutdown();
        menu.Items.Add(cap);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quit);
        tray.Menu = menu;

        TrayIcon.SetIcons(this, new TrayIcons { tray });
    }

    private void Shutdown()
    {
        _hotkey?.Dispose();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
            d.Shutdown();
    }

    private void Capture()
    {
        if (_capturing || _host == null) return;
        _capturing = true;
        try
        {
            var all = _host.Screens.All;
            if (all == null || all.Count == 0) { _capturing = false; return; }

            // 覆盖鼠标所在的那个显示器（单窗口最稳，且弹在你正操作的屏上）
            var cur = PlatformServices.Current.CursorPosition();
            var screen = (cur.HasValue ? _host.Screens.ScreenFromPoint(cur.Value) : null)
                         ?? _host.Screens.Primary ?? all[0];
            var bounds = screen.Bounds;
            var shot = PlatformServices.Current.CaptureRegion(bounds);
            var win = new OverlayWindow(shot, bounds, screen.Scaling);
            win.Closed += (_, _) => _capturing = false;
            win.Show();
            win.Activate();
        }
        catch { _capturing = false; }
    }
}
