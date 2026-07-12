using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SkiaSharp;

namespace AltSnip;

/// <summary>无边框窗，铺满整个虚拟屏（所有显示器）。</summary>
public sealed class OverlayWindow : Window
{
    readonly OverlayControl _control;
    readonly PixelRect _bounds;

    public OverlayWindow(SKBitmap shot, PixelRect bounds, double scaling)
    {
        _bounds = bounds;
        SystemDecorations = SystemDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Black;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = bounds.Position;
        Width = bounds.Width / scaling;     // DIP 尺寸 = 物理 / 缩放
        Height = bounds.Height / scaling;

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
        Position = _bounds.Position;   // 某些后端 Show 后会挪位，重新钉住
        Activate();
        _control.Focus();
    }
}
