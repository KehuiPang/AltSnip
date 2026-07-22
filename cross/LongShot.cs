using System;
using System.IO;
using System.Threading.Tasks;
using WuweiShot.Platform;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;

namespace WuweiShot;

/// <summary>长截图（微信式手动滚）：遮罩关闭后，用户自己向下滚真 App，
/// 后台定时抓选区 + 增量拼接，右侧单框实时预览，选区外右下角 ✓完成(复制)/✕取消。
/// 完成弹一个"无标题栏/无按钮"的纯长图预览窗。</summary>
public static class LongShot
{
    public static void Run(PixelRect region, SKBitmap frame0, double scaling, PixelRect screenBounds)
        => new LongSession(region, frame0, scaling <= 0 ? 1 : scaling, screenBounds).Start();

    // ---- SKBitmap → Avalonia Bitmap（可选降采样，预览用小图省开销）----
    internal static Bitmap ToAvalonia(SKBitmap sk, int maxW = 0, int maxH = 0)
    {
        SKBitmap use = sk; bool tmp = false;
        if (maxW > 0 && maxH > 0 && (sk.Width > maxW || sk.Height > maxH))
        {
            double k = Math.Min((double)maxW / sk.Width, (double)maxH / sk.Height);
            int nw = Math.Max(1, (int)(sk.Width * k)), nh = Math.Max(1, (int)(sk.Height * k));
            use = sk.Resize(new SKImageInfo(nw, nh), SKFilterQuality.Medium) ?? sk;
            tmp = use != sk;
        }
        using var img = SKImage.FromBitmap(use);
        using var data = img.Encode(SKEncodedImageFormat.Png, 90);
        using var ms = new MemoryStream();
        data.SaveTo(ms); ms.Position = 0;
        var bmp = new Bitmap(ms);
        if (tmp) use.Dispose();
        return bmp;
    }
}

internal sealed class LongSession
{
    readonly PixelRect _region;
    readonly PixelRect _screen;
    readonly double _scale;
    readonly Stitcher.Accumulator _acc;
    readonly IPlatformServices _plat = PlatformServices.Current;

    DimWindow? _dim;
    BorderWindow? _border;
    PreviewWindow? _preview;
    HintWindow? _hint;
    ControlWindow? _control;
    bool _running;
    bool _busyPreview;
    DateTime _lastPreview = DateTime.MinValue;

    public LongSession(PixelRect region, SKBitmap frame0, double scale, PixelRect screen)
    {
        _region = region; _scale = scale; _screen = screen;
        _acc = new Stitcher.Accumulator(frame0);
    }

    public void Start()
    {
        // 选区(物理像素) → DIP 位置/尺寸
        double s = _scale;
        var pos = _region.Position;
        double wDip = _region.Width / s, hDip = _region.Height / s;
        int cxp = _region.X + _region.Width / 2, cyp = _region.Y + _region.Height / 2;

        // 控件条：仿微信——右对齐选区右边缘、置于选区正下方（无背景框，三个实心胶囊图标键浮在暗蒙层上）
        const int CtlW = 140, CtlH = 44;
        int ctlX = pos.X + _region.Width - (int)(CtlW * s);            // 右对齐选区右边
        ctlX = Math.Max(_screen.X + 4, ctlX);
        int ctlY = pos.Y + _region.Height + 8;
        if (ctlY + (int)(CtlH * s) > _screen.Y + _screen.Height)
            ctlY = pos.Y + _region.Height - (int)(CtlH * s) - 8;       // 贴底则收进选区内右下
        var ctlPos = new PixelPoint(ctlX, ctlY);

        // 右侧实时预览：物理区域也先算好——蒙层同样在这挖洞，预览才不被 50% 黑蒙压暗（治"预览发灰/被遮"）
        double prevW = Math.Min(150, Math.Max(96, wDip * 0.4));
        var prevPos = new PixelPoint(pos.X + _region.Width + 12, pos.Y);
        var prevPhys = new PixelRect(prevPos.X, prevPos.Y, (int)Math.Ceiling(prevW * s), _region.Height);

        // "滚动截取更多内容"提示文字：左对齐选区左边、悬于选区正上方（无背景框，纯文字浮在暗蒙层上）
        const int HintW = 240, HintH = 28;
        int hintX = pos.X;                                            // 左对齐选区左边
        int hintY = Math.Max(_screen.Y + 4, pos.Y - (int)(HintH * s) - 8);
        var hintPos = new PixelPoint(hintX, hintY);

        // 选区外暗色蒙层（点击穿透；仅选区 + 右侧预览挖空亮出；提示/按钮改为浮在暗蒙层上，不再挖洞）
        _dim = new DimWindow(_screen, _region, prevPhys, s);
        _dim.Show();
        _border = new BorderWindow(new PixelPoint(pos.X - 3, pos.Y - 3), wDip + 6, hDip + 6);
        _border.Show();

        // 此刻：上一层全屏取景窗(实心黑、非穿透)已关闭；我方 dim/border 是 WS_EX_TRANSPARENT
        // (WindowFromPoint 会跳过穿透窗) → 取选区中心下方拿到的正是要截的真 App，而非残留浮层。
        IntPtr target = WinScroll.WindowUnder(cxp, cyp);

        _preview = new PreviewWindow(prevPos, prevW, hDip);
        _preview.Show();
        _hint = new HintWindow(hintPos, HintW, HintH);
        _hint.Show();
        _control = new ControlWindow(ctlPos, CtlW, CtlH, Finish, Save, Cancel);
        _control.OnCancelKey = Cancel;
        _control.Show();

        // 浮层都摆好后，把目标窗口(顶层)顶到前台取得焦点 → 用户原生滚轮直接滚它
        WinScroll.FocusTarget(target);
        // 提示/按钮无洞、浮在蒙层上：需确保其 z 序在蒙层之上（否则被 50% 黑压暗）。
        // 蒙层 OnOpened 有一次延迟置顶，这里补一次更晚的置顶把浮层顶回最上。
        Dispatcher.UIThread.Post(() => { _hint?.Bump(); _control?.Bump(); _preview?.Bump(); },
            DispatcherPriority.Background);
        _running = true;
        UpdatePreview();   // 立即显示首帧(选区当前内容)，"未滚动=全黑"不再误判为坏
        _ = Loop();
    }

    async Task Loop()
    {
        await Task.Delay(320);   // 等遮罩消失 + 目标重绘
        while (_running)
        {
            bool changed = await Task.Run(() =>
            {
                SKBitmap f;
                try { f = _plat.CaptureRegion(_region); } catch { return false; }
                bool c;
                try { c = _acc.Feed(f); } catch { c = false; }
                f.Dispose();
                return c;
            });
            if (!_running) break;
            if (changed) UpdatePreview();
            if (_acc.Height > 30000) { Finish(); return; }   // 安全上限
            await Task.Delay(140);
        }
    }

    void UpdatePreview()
    {
        if (_busyPreview || (DateTime.UtcNow - _lastPreview).TotalMilliseconds < 260) return;
        _busyPreview = true; _lastPreview = DateTime.UtcNow;
        _ = Task.Run(() =>
        {
            Bitmap? bmp = null;
            try { using var full = _acc.Compose(); bmp = LongShot.ToAvalonia(full, 300, 4000); } catch { }
            Dispatcher.UIThread.Post(() =>
            {
                if (_running && bmp != null) _preview?.Update(bmp);
                else bmp?.Dispose();
                _busyPreview = false;
            });
        });
    }

    void Finish()
    {
        if (!_running) return;
        _running = false;
        SKBitmap result;
        try { result = _acc.Compose(); } catch { CloseChrome(); return; }
        try { _plat.CopyImageToClipboard(SKImage.FromBitmap(result)); } catch { }
        CloseChrome();
        try { new ResultWindow(result).Show(); } catch { }
    }

    // 保存：停止拼接 → 结果窗打开并立刻弹"另存为"，写 PNG 到用户选的位置
    void Save()
    {
        if (!_running) return;
        _running = false;
        SKBitmap result;
        try { result = _acc.Compose(); } catch { CloseChrome(); return; }
        CloseChrome();
        try { var w = new ResultWindow(result); w.Show(); _ = w.SaveAsync(); } catch { }
    }

    void Cancel()
    {
        _running = false;
        CloseChrome();
        _acc.Dispose();
    }

    void CloseChrome()
    {
        try { _dim?.Close(); } catch { }
        try { _border?.Close(); } catch { }
        try { _preview?.Close(); } catch { }
        try { _hint?.Close(); } catch { }
        try { _control?.Close(); } catch { }
        _dim = null; _border = null; _preview = null; _hint = null; _control = null;
    }
}

// ---- 选区外暗色蒙层：铺满目标屏、选区处挖空；透明+点击穿透（滚轮穿过去滚真 App）----
internal sealed class DimWindow : Window
{
    readonly PixelPoint _origin;   // 目标屏物理原点，OnOpened 时再校正（PerMonitorV2 下 ctor 落点不可靠）
    readonly PixelRect _screenPx;  // 目标屏物理矩形：OnOpened 用它强制原生窗口矩形，DPI-proof
    const double Bleed = 8;        // 外沿溢出(DIP)：整体越过窗口边一点，任何取整误差都不会在屏边留缝

    // 用"外框铺满 + EvenOdd 挖洞"画蒙层：外框(含 bleed)算 1 层，洞区再被内矩形覆盖成 2 层(偶数)=不填充=透明。
    // 天然无拼接缝(治 #1 顶/左盖不满)，且能同时挖多个洞(选区洞 + 按钮洞，治 #2 按钮被压暗)。
    public DimWindow(PixelRect screen, PixelRect region, PixelRect preview, double scale)
    {
        SystemDecorations = SystemDecorations.None;
        CanResize = false; ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        WindowStartupLocation = WindowStartupLocation.Manual;
        _origin = screen.Position;
        _screenPx = screen;
        Position = _origin;
        double fw = screen.Width / scale, fh = screen.Height / scale;
        Width = fw; Height = fh;

        // 物理坐标 → 本窗 DIP（洞的四边保持精确）
        Rect Hole(PixelRect r) => new Rect(
            (r.X - screen.X) / scale, (r.Y - screen.Y) / scale, r.Width / scale, r.Height / scale);
        var sel = Hole(region);
        var prev = Hole(preview);             // 预览洞：右侧预览条按原样亮出，不被蒙层压暗

        double m = Bleed;
        var geo = new GeometryGroup { FillRule = FillRule.EvenOdd };
        geo.Children.Add(new RectangleGeometry(new Rect(-m, -m, fw + 2 * m, fh + 2 * m))); // 外框(bleed)
        geo.Children.Add(new RectangleGeometry(sel));                                       // 选区洞
        geo.Children.Add(new RectangleGeometry(prev));                                      // 预览洞
        var path = new Avalonia.Controls.Shapes.Path
        {
            Fill = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),   // 半透明黑 ~50%
            Data = geo,
        };
        var cv = new Canvas();
        Canvas.SetLeft(path, 0); Canvas.SetTop(path, 0);
        cv.Children.Add(path);
        Content = cv;
    }
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // 用物理像素强制窗口 = 目标屏矩形，绕开 Avalonia 的 DPI 定位误差 →
        // 蒙层严丝合缝盖满整屏，不再顶部/左侧漏一条。
        var h = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        WinOverlay.ApplyRect(h, clickThrough: true,
            _screenPx.X, _screenPx.Y, _screenPx.Width, _screenPx.Height);
        // 蒙层铺满后，把洞区精确裁回目标尺寸，避免 bleed 外沿被强设物理矩形后变成黑边
        Dispatcher.UIThread.Post(() =>
        {
            if (h != IntPtr.Zero)
            {
                WinOverlay.ApplyRect(h, clickThrough: true,
                    _screenPx.X, _screenPx.Y, _screenPx.Width, _screenPx.Height);
            }
        }, DispatcherPriority.Background);
    }
}

// ---- 选区外框：透明、点击穿透（滚轮穿过去滚真 App），只画一圈竹青边 ----
internal sealed class BorderWindow : Window
{
    readonly PixelPoint _origin;
    public BorderWindow(PixelPoint pos, double wDip, double hDip)
    {
        SystemDecorations = SystemDecorations.None;
        CanResize = false; ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        WindowStartupLocation = WindowStartupLocation.Manual;
        _origin = pos; Position = pos; Width = wDip; Height = hDip;
        Content = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x5C, 0x8A, 0x73)),
            BorderThickness = new Thickness(2),
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(3),
        };
    }
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Position = _origin;
        WinOverlay.Apply(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero, clickThrough: true);
    }
}

// ---- 实时预览：右侧单框，只显示正在拼的长图（底部对齐，越长越往上滚）----
internal sealed class PreviewWindow : Window
{
    readonly Image _img;
    readonly PixelPoint _origin;
    public PreviewWindow(PixelPoint pos, double wDip, double hDip)
    {
        SystemDecorations = SystemDecorations.None;
        CanResize = false; ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        // 全透明：去掉深色底/深色边框，预览只留截图本身，与选区一样亮
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        WindowStartupLocation = WindowStartupLocation.Manual;
        _origin = pos; Position = pos; Width = wDip; Height = hDip;
        _img = new Image { Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Bottom };
        Content = _img;
    }
    public void Update(Bitmap bmp)
    {
        var old = _img.Source as Bitmap;
        _img.Source = bmp;
        old?.Dispose();
    }
    public void Bump() => WinOverlay.Apply(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero, clickThrough: false);
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Position = _origin;
        WinOverlay.Apply(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero, clickThrough: false);
    }
}

// ---- 长截图统一视觉：无为 VI 深色胶囊条 + 矢量线性图标（治"字符图标"粗糙问题）----
internal static class LongUi
{
    public static readonly Color Ink     = Color.FromArgb(0xEB, 0x16, 0x19, 0x1E); // 半透明玄墨底
    public static readonly Color Neutral = Color.FromRgb(0xC7, 0xCE, 0xD4);        // 月白偏银灰（中性图标）
    public static readonly Color Accent  = Color.FromRgb(0xC0, 0x5F, 0x3C);        // 朱赭"一念"（主操作）

    // 20×20 视框内的线性图标路径（描边、圆头端点，合无为 VI 图标语言）
    public const string IcoClose = "M6,6 L14,14 M14,6 L6,14";
    public const string IcoCheck = "M5,10.4 L8.6,14 L15,6.6";
    public const string IcoSave  = "M10,3 L10,12 M6.3,8.4 L10,12 L13.7,8.4 M4,15.8 L16,15.8";
    public const string IcoCopy  = "M4.5,8 L12.5,8 L12.5,17 L4.5,17 Z M7.5,8 L7.5,4 L15.5,4 L15.5,13 L12.5,13";

    static readonly Color Hover = Color.FromRgb(0x2A, 0x31, 0x3A);   // hover/分隔线色

    static Avalonia.Controls.Shapes.Path Stroke15(string data, Color c, double w = 1.5) => new()
    {
        Data = Geometry.Parse(data),
        Stroke = new SolidColorBrush(c),
        StrokeThickness = w,
        StrokeLineCap = PenLineCap.Round,
        StrokeJoin = PenLineJoin.Round,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    static readonly Color Chip = Color.FromRgb(0x1E, 0x23, 0x2B);   // 实心胶囊底（玄墨卡片色）

    // 中性键：实心玄墨胶囊 + 细描边图标，hover 提亮
    public static Button IconBtn(string data, Color stroke)
    {
        var baseBg = new SolidColorBrush(Chip);
        var hoverBg = new SolidColorBrush(Hover);
        var btn = new Button
        {
            Content = Stroke15(data, stroke),
            Width = 40, Height = 32,
            Background = baseBg,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        btn.PointerEntered += (_, _) => btn.Background = hoverBg;
        btn.PointerExited += (_, _) => btn.Background = baseBg;
        return btn;
    }

    // 主操作：实心朱赭胶囊 + 白色图标（纯图标，无文字）
    public static Button PrimaryBtn(string data)
    {
        var accent = new SolidColorBrush(Accent);
        var accentHover = new SolidColorBrush(Color.FromRgb(0xCF, 0x6B, 0x46));   // 略提亮
        var btn = new Button
        {
            Content = Stroke15(data, Color.FromRgb(0xFF, 0xFF, 0xFF), 1.7),
            Width = 40, Height = 32,
            Background = accent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        btn.PointerEntered += (_, _) => btn.Background = accentHover;
        btn.PointerExited += (_, _) => btn.Background = accent;
        return btn;
    }

    // 胶囊工具条：半透明玄墨底 + 圆角 + 柔和投影（固定尺寸避免高 DPI 布局抖动）
    public static Border Bar(Control child, double w, double h) => new Border
    {
        Background = new SolidColorBrush(Ink),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(8, 5),
        Width = w, Height = h,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        BoxShadow = BoxShadows.Parse("0 2 12 0 #55000000"),
        Child = child,
    };
}

// ---- "滚动截取更多内容"提示气泡：悬于选区上方居中，点击穿透 ----
internal sealed class HintWindow : Window
{
    readonly PixelPoint _origin;
    public HintWindow(PixelPoint pos, double wDip, double hDip)
    {
        SystemDecorations = SystemDecorations.None;
        CanResize = false; ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        WindowStartupLocation = WindowStartupLocation.Manual;
        _origin = pos; Position = pos; Width = wDip; Height = hDip;
        // 无背景框：纯文字左对齐浮在暗蒙层上（加一点半透明黑描边阴影保证在浅色区也读得清）
        Content = new TextBlock
        {
            Text = "滚动页面截取更多内容",
            Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xF8)),
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }
    public void Bump() => WinOverlay.Apply(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero, clickThrough: true);
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Position = _origin;
        WinOverlay.Apply(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero, clickThrough: true);
    }
}

// ---- 选区下方居中控件条：取消 / 保存 / 完成（矢量图标，仿微信居中）----
internal sealed class ControlWindow : Window
{
    public Action? OnCancelKey;
    readonly PixelPoint _origin;
    public ControlWindow(PixelPoint pos, int wDip, int hDip, Action onOk, Action onSave, Action onCancel)
    {
        SystemDecorations = SystemDecorations.None;
        CanResize = false; ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        WindowStartupLocation = WindowStartupLocation.Manual;
        _origin = pos; Position = pos; Width = wDip; Height = hDip;

        // 无为 VI：取消/保存=银灰中性幽灵键，完成=实心朱赭主按钮
        var no = LongUi.IconBtn(LongUi.IcoClose, LongUi.Neutral);
        no.Click += (_, _) => onCancel();
        var save = LongUi.IconBtn(LongUi.IcoSave, LongUi.Neutral);
        save.Click += (_, _) => onSave();
        var ok = LongUi.PrimaryBtn(LongUi.IcoCheck);
        ok.Click += (_, _) => onOk();

        // 无外框：三个实心胶囊图标键右对齐浮在暗蒙层上
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { no, save, ok },
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) OnCancelKey?.Invoke(); };
    }

    public void Bump() => WinOverlay.Apply(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero, clickThrough: false);

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Position = _origin;
        WinOverlay.Apply(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero, clickThrough: false);
    }
}

// ---- 结果框：长图 + 底部悬浮工具条（保存到文件 / 复制 / 关闭）；Esc 关闭 ----
internal sealed class ResultWindow : Window
{
    readonly SKBitmap _img;
    public ResultWindow(SKBitmap img)
    {
        _img = img;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = true; Topmost = false;
        Background = new SolidColorBrush(Color.FromRgb(0x16, 0x19, 0x1E));   // 玄墨底
        Width = Math.Min(780, img.Width + 24);
        Height = 760;

        var image = new Image { Source = LongShot.ToAvalonia(img), Stretch = Stretch.None };
        var scroll = new ScrollViewer
        {
            Content = image,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Padding = new Thickness(8),
        };

        var close = LongUi.IconBtn(LongUi.IcoClose, LongUi.Neutral);
        close.Click += (_, _) => Close();
        var save = LongUi.IconBtn(LongUi.IcoSave, LongUi.Neutral);
        save.Click += async (_, _) => await SaveAsync();
        var copy = LongUi.PrimaryBtn(LongUi.IcoCopy);
        copy.Click += (_, _) => DoCopy();

        // 底部悬浮：三个实心胶囊图标键，居中
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 16),
            Children = { close, save, copy },
        };

        Content = new Panel { Children = { scroll, bar } };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    public async Task SaveAsync()
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "保存长截图",
                SuggestedFileName = "长截图.png",
                DefaultExtension = "png",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("PNG 图片") { Patterns = new[] { "*.png" } },
                },
            });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            using var im = SKImage.FromBitmap(_img);
            using var d = im.Encode(SKEncodedImageFormat.Png, 95);
            d.SaveTo(stream);
        }
        catch { }
    }

    void DoCopy()
    {
        try { PlatformServices.Current.CopyImageToClipboard(SKImage.FromBitmap(_img)); } catch { }
    }
}
