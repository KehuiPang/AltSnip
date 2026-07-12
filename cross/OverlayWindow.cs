using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SkiaSharp;

namespace AltSnip;

/// <summary>覆盖单个屏幕的无边框全屏取景窗。用 FullScreen 精确铺满屏幕，避免手动 DPI 换算。</summary>
public sealed class OverlayWindow : Window
{
    readonly OverlayControl _control;
    readonly PixelRect _bounds;

    public OverlayWindow(SKBitmap shot, PixelRect bounds)
    {
        _bounds = bounds;
        SystemDecorations = SystemDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Black;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = bounds.Position;   // 先落到目标屏幕，再全屏

        var textLayer = new Canvas();
        _control = new OverlayControl(shot, Close, Copy) { TextLayer = textLayer };
        var grid = new Grid();
        grid.Children.Add(_control);
        grid.Children.Add(textLayer);
        Content = grid;
    }

    void Copy(SKImage image)
    {
        try { Platform.PlatformServices.Current.CopyImageToClipboard(image); } catch { }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Position = _bounds.Position;
        WindowState = WindowState.FullScreen;   // 精确铺满该屏幕（含任务栏区域）
        Activate();
        _control.Focus();
    }
}
