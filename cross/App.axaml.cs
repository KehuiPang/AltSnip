using System;
using AltSnip.Platform;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace AltSnip;

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
            foreach (var a in Environment.GetCommandLineArgs())
                if (a == "--test-capture")
                    Avalonia.Threading.Dispatcher.UIThread.Post(Capture,
                        Avalonia.Threading.DispatcherPriority.Background);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static WindowIcon LoadIcon()
        => new WindowIcon(AssetLoader.Open(new Uri("avares://AltSnip/Assets/logo.png")));

    private void SetupTray()
    {
        var tray = new TrayIcon { ToolTipText = "AltSnip — Alt+A to capture", Icon = LoadIcon() };
        tray.Clicked += (_, _) => Capture();

        var menu = new NativeMenu();
        var cap = new NativeMenuItem("Capture (Alt+A)");
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

            // 覆盖整个虚拟屏（所有显示器的并集），像经典版那样，随处可框选
            int l = int.MaxValue, t = int.MaxValue, r = int.MinValue, b = int.MinValue;
            foreach (var s in all)
            {
                var bd = s.Bounds;
                l = Math.Min(l, bd.X); t = Math.Min(t, bd.Y);
                r = Math.Max(r, bd.X + bd.Width); b = Math.Max(b, bd.Y + bd.Height);
            }
            var vbounds = new PixelRect(l, t, r - l, b - t);
            double scaling = _host.Screens.Primary?.Scaling ?? all[0].Scaling;

            var shot = PlatformServices.Current.CaptureRegion(vbounds);
            var win = new OverlayWindow(shot, vbounds, scaling);
            win.Closed += (_, _) => _capturing = false;
            win.Show();
            win.Activate();
        }
        catch { _capturing = false; }
    }
}
