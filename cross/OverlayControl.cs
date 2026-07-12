using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace AltSnip;

/// <summary>
/// 全屏取景层：显示冻结截图（压暗），拖框选区。M1 只做框选 + 复制；
/// 标注(箭头/文字/马赛克等)在 M3 加入。
/// </summary>
public sealed class OverlayControl : Control
{
    readonly SKBitmap _src;          // 物理像素原图
    readonly Bitmap _display;        // 显示用 Avalonia 位图
    readonly Action _onCancel;
    readonly Action<SKImage> _onConfirm;

    bool _dragging, _hasSel;
    Point _start;
    Rect _sel;

    public OverlayControl(SKBitmap src, Action onCancel, Action<SKImage> onConfirm)
    {
        _src = src;
        _onCancel = onCancel;
        _onConfirm = onConfirm;
        _display = ToAvaloniaBitmap(src);
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    static Bitmap ToAvaloniaBitmap(SKBitmap sk)
    {
        using var img = SKImage.FromBitmap(sk);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        using var ms = new MemoryStream();
        data.SaveTo(ms);
        ms.Position = 0;
        return new Bitmap(ms);
    }

    double ScaleX => Bounds.Width > 0 ? _src.Width / Bounds.Width : 1;
    double ScaleY => Bounds.Height > 0 ? _src.Height / Bounds.Height : 1;

    static Rect RectFrom(Point a, Point b)
        => new Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) { _onCancel(); return; }
        _dragging = true;
        _hasSel = false;
        _start = e.GetPosition(this);
        _sel = new Rect(_start, new Size(0, 0));
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_dragging) return;
        _sel = RectFrom(_start, e.GetPosition(this));
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        _hasSel = _sel.Width >= 3 && _sel.Height >= 3;
        if (!_hasSel) _sel = default;
        InvalidateVisual();
    }

    public void Confirm()
    {
        if (!_hasSel) return;
        int l = Math.Max(0, (int)(_sel.X * ScaleX));
        int t = Math.Max(0, (int)(_sel.Y * ScaleY));
        int r = Math.Min(_src.Width, (int)(_sel.Right * ScaleX));
        int b = Math.Min(_src.Height, (int)(_sel.Bottom * ScaleY));
        if (r <= l || b <= t) return;

        var sub = new SKBitmap(r - l, b - t);
        if (_src.ExtractSubset(sub, new SKRectI(l, t, r, b)))
            _onConfirm(SKImage.FromBitmap(sub));
    }

    public override void Render(DrawingContext ctx)
    {
        var full = new Rect(Bounds.Size);
        ctx.DrawImage(_display, full);
        ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), full);

        if (_sel.Width > 0 && _sel.Height > 0)
        {
            using (ctx.PushClip(_sel))
                ctx.DrawImage(_display, full);
            var gold = new SolidColorBrush(Color.FromRgb(0xf4, 0xb7, 0x40));
            ctx.DrawRectangle(null, new Pen(gold, 1.5), _sel);
        }
        else if (!_dragging)
        {
            var tip = new FormattedText("拖动框选区域    ·    Enter 复制    ·    Esc 取消",
                System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                Typeface.Default, 15, Brushes.White);
            var bg = new SolidColorBrush(Color.FromArgb(180, 14, 10, 6));
            double tx = (Bounds.Width - tip.Width) / 2, ty = 44;
            ctx.FillRectangle(bg, new Rect(tx - 16, ty - 8, tip.Width + 32, tip.Height + 16), 8);
            ctx.DrawText(tip, new Point(tx, ty));
        }
    }
}
