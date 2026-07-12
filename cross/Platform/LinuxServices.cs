using System;
using System.IO;
using Avalonia;
using SkiaSharp;

namespace AltSnip.Platform;

/// <summary>Linux 实现：grim/scrot/ImageMagick 截屏 + xclip/wl-copy 图片剪贴板。全局热键待 M2。</summary>
public sealed class LinuxServices : IPlatformServices
{
    public string Name => "linux";

    public SKBitmap CaptureRegion(PixelRect region)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "altsnip_capture.png");
        int w = Math.Max(1, region.Width), h = Math.Max(1, region.Height);

        // Wayland：grim 支持直接抠区域
        if (Proc.Exists("grim") &&
            Proc.Run("grim", "-g", $"{region.X},{region.Y} {w}x{h}", tmp) == 0 && File.Exists(tmp))
            return SKBitmap.Decode(tmp) ?? new SKBitmap(w, h);

        // X11：整屏抓下来再裁
        bool full = false;
        if (Proc.Exists("scrot")) full = Proc.Run("scrot", "-o", tmp) == 0;
        if (!full && Proc.Exists("import")) full = Proc.Run("import", "-window", "root", tmp) == 0;
        if (!full && Proc.Exists("gnome-screenshot")) full = Proc.Run("gnome-screenshot", "-f", tmp) == 0;

        var shot = File.Exists(tmp) ? SKBitmap.Decode(tmp) : null;
        if (shot == null) return new SKBitmap(w, h);
        if (region.X == 0 && region.Y == 0 && shot.Width == w && shot.Height == h) return shot;

        var crop = new SKBitmap(w, h, shot.ColorType, shot.AlphaType);
        using (var canvas = new SKCanvas(crop))
            canvas.DrawBitmap(shot, new SKRect(region.X, region.Y, region.X + w, region.Y + h),
                              new SKRect(0, 0, w, h));
        shot.Dispose();
        return crop;
    }

    public void CopyImageToClipboard(SKImage image)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "altsnip_copy.png");
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var fs = File.Create(tmp))
            data.SaveTo(fs);

        if (Proc.Exists("wl-copy")) { Proc.RunWithStdin("wl-copy", tmp, "--type", "image/png"); return; }
        if (Proc.Exists("xclip")) { Proc.Run("xclip", "-selection", "clipboard", "-t", "image/png", "-i", tmp); return; }
    }

    public IDisposable? RegisterHotkey(Action onAltA) => null; // M2: X11 XGrabKey
}
