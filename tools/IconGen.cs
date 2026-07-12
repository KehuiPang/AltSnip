// AltSnip 图标/Logo 生成器 —— 按「无为」品牌 VI 绘制。
// 产品图标规范：玄墨/靛青渐变方圆底 + 居中月白「○带缺口」(一念之门·圆相) + 缺口一点朱赭"一念"火种。
// 色板：玄墨黑 #16191E / 靛青 #274A63 / 月白 #F4F6F8 / 银灰 #B7C0C7 / 朱赭 #C05F3C。
// 编译： csc /target:exe /out:IconGen.exe /reference:System.Drawing.dll tools\IconGen.cs
// 运行： IconGen.exe <输出目录>
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class IconGen
{
    // 无为 VI
    static readonly Color INK    = Hex("#16191E"); // 玄墨黑
    static readonly Color INK2   = Hex("#22384A"); // 玄墨→靛青（压深）
    static readonly Color MOON   = Hex("#F4F6F8"); // 月白
    static readonly Color SILVER = Hex("#AEB8C0"); // 银灰（圆环渐隐）
    static readonly Color SPARK  = Hex("#C05F3C"); // 朱赭·一念火种

    static Color Hex(string h)
    {
        h = h.TrimStart('#');
        return Color.FromArgb(
            Convert.ToInt32(h.Substring(0, 2), 16),
            Convert.ToInt32(h.Substring(2, 2), 16),
            Convert.ToInt32(h.Substring(4, 2), 16));
    }

    static Bitmap DrawLogo(int S)
    {
        var bmp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // 方圆底：玄墨黑 → 靛青 对角渐变
            float radius = 58 * S / 256f;
            var tile = new RectangleF(0.5f, 0.5f, S - 1f, S - 1f);
            using (var path = Rounded(tile, radius))
            using (var br = new LinearGradientBrush(new PointF(0, 0), new PointF(S, S), INK, INK2))
                g.FillPath(br, path);

            // 一念之门·圆相：月白圆环，右下留气口
            float cx = S / 2f, cy = S / 2f;
            float ringR = S * 0.30f;
            float sw = S * 0.072f;
            var ringRect = new RectangleF(cx - ringR, cy - ringR, ringR * 2, ringR * 2);
            float startAngle = 70f, sweep = 300f;   // 缺口约 60°，在右下
            using (var rb = new LinearGradientBrush(
                new PointF(cx - ringR, cy - ringR), new PointF(cx + ringR, cy + ringR), MOON, SILVER))
            using (var pen = new Pen(rb, sw) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(pen, ringRect, startAngle, sweep);

            // 缺口下端一点朱赭（含柔光）
            double a = startAngle * Math.PI / 180.0;
            float dx = cx + (float)(Math.Cos(a) * ringR);
            float dy = cy + (float)(Math.Sin(a) * ringR);
            float dotR = sw * 0.60f;
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(dx - dotR * 2.6f, dy - dotR * 2.6f, dotR * 5.2f, dotR * 5.2f);
                using (var pgb = new PathGradientBrush(glow)
                {
                    CenterColor = Color.FromArgb(120, SPARK),
                    SurroundColors = new[] { Color.FromArgb(0, SPARK) },
                    CenterPoint = new PointF(dx, dy),
                })
                    g.FillPath(pgb, glow);
            }
            using (var db = new SolidBrush(SPARK))
                g.FillEllipse(db, dx - dotR, dy - dotR, dotR * 2, dotR * 2);
        }
        return bmp;
    }

    static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : ".";
        int[] sizes = { 256, 128, 64, 48, 32, 16 };
        var pngs = new List<byte[]>();
        foreach (int s in sizes)
        {
            using (var bmp = DrawLogo(s))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                pngs.Add(ms.ToArray());
                if (s == 256) bmp.Save(Path.Combine(outDir, "logo_256.png"), ImageFormat.Png);
            }
        }

        string icoPath = Path.Combine(outDir, "app.ico");
        using (var fs = File.Create(icoPath))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0);
            w.Write((short)1);
            w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                byte wh = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                w.Write(wh); w.Write(wh);
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(pngs[i].Length); w.Write(offset);
                offset += pngs[i].Length;
            }
            foreach (var png in pngs) w.Write(png);
        }
        Console.WriteLine("Wrote " + icoPath + " and logo_256.png");
    }
}
