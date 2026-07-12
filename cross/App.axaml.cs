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
            var screen = _host.Screens.Primary
                         ?? (_host.Screens.All.Count > 0 ? _host.Screens.All[0] : null);
            if (screen == null) { _capturing = false; return; }

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
