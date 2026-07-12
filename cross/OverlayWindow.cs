using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SkiaSharp;

namespace AltSnip;

/// <summary>覆盖单个屏幕的无边框全屏取景窗。</summary>
public sealed class OverlayWindow : Window
{
    readonly OverlayControl _control;

    public OverlayWindow(SKBitmap shot, PixelRect bounds, double scaling)
    {
        SystemDecorations = SystemDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Black;
        Position = bounds.Position;
        Width = bounds.Width / scaling;
        Height = bounds.Height / scaling;

        _control = new OverlayControl(shot, Close, CopyAndClose);
        Content = _control;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
            else if (e.Key == Key.Enter || e.Key == Key.Return) _control.Confirm();
        };
    }

    void CopyAndClose(SKImage image)
    {
        try { Platform.PlatformServices.Current.CopyImageToClipboard(image); } catch { }
        Close();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Activate();
        _control.Focus();
    }
}
