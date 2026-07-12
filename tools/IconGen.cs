// AltSnip 图标/Logo 生成器 —— 按 OPC「暖琥珀」品牌 VI 绘制。
// 概念：暖墨圆角底 + 金色取景框四角标（截图/选区语义）+ 橙环金点快门。
// 金=点睛，橙=结构线，深底留白。生成多尺寸 app.ico 与 logo_256.png。
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
    // 暖琥珀 VI
    static readonly Color BG1 = ColorFromHex("#171109");
    static readonly Color BG2 = ColorFromHex("#0b0805");
    static readonly Color GOLD = ColorFromHex("#f4b740");
    static readonly Color ORANGE = ColorFromHex("#e0762a");

    static Color ColorFromHex(string h)
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
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            float f = S / 256f;
            float radius = 56 * f;
            var tile = new RectangleF(0.5f, 0.5f, S - 1f, S - 1f);

            // 暖墨圆角底（对角渐变）
            using (var path = Rounded(tile, radius))
            {
                using (var br = new LinearGradientBrush(
                    new PointF(0, 0), new PointF(S, S), BG1, BG2))
                    g.FillPath(br, path);
                // 极细橙色结构描边
                using (var pen = new Pen(Color.FromArgb(70, ORANGE), Math.Max(1f, 1.2f * f)))
                    g.DrawPath(pen, path);
            }

            // 取景框四角标（金）
            float inset = 62 * f;
            float arm = 40 * f;
            float stroke = 18 * f;
            using (var pen = new Pen(GOLD, stroke))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                float lo = inset, hi = S - inset;
                Bracket(g, pen, new PointF(lo, lo), new PointF(lo + arm, lo), new PointF(lo, lo + arm)); // 左上
                Bracket(g, pen, new PointF(hi, lo), new PointF(hi - arm, lo), new PointF(hi, lo + arm)); // 右上
                Bracket(g, pen, new PointF(lo, hi), new PointF(lo + arm, hi), new PointF(lo, hi - arm)); // 左下
                Bracket(g, pen, new PointF(hi, hi), new PointF(hi - arm, hi), new PointF(hi, hi - arm)); // 右下
            }

            // 快门：橙色结构环 + 金色点睛（仅大尺寸画环，小尺寸只留金点保清晰）
            float cx = S / 2f, cy = S / 2f;
            if (S >= 48)
            {
                float ring = 26 * f;
                using (var pen = new Pen(ORANGE, 9 * f))
                    g.DrawEllipse(pen, cx - ring, cy - ring, ring * 2, ring * 2);
            }
            float dot = (S >= 48 ? 12f : 20f) * f;
            using (var br = new SolidBrush(GOLD))
                g.FillEllipse(br, cx - dot, cy - dot, dot * 2, dot * 2);
        }
        return bmp;
    }

    static void Bracket(Graphics g, Pen pen, PointF elbow, PointF a, PointF b)
    {
        using (var p = new GraphicsPath())
        {
            p.AddLine(a, elbow);
            p.AddLine(elbow, b);
            g.DrawPath(pen, p);
        }
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
            w.Write((short)0);            // reserved
            w.Write((short)1);            // type = icon
            w.Write((short)sizes.Length); // count
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                byte wh = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                w.Write(wh); w.Write(wh); // width, height
                w.Write((byte)0);         // colors
                w.Write((byte)0);         // reserved
                w.Write((short)1);        // planes
                w.Write((short)32);       // bpp
                w.Write(pngs[i].Length);  // size
                w.Write(offset);          // offset
                offset += pngs[i].Length;
            }
            foreach (var png in pngs) w.Write(png);
        }
        Console.WriteLine("Wrote " + icoPath + " and logo_256.png");
    }
}
