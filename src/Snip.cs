using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SnipTool
{
    // 托盘常驻程序：用底层键盘钩子(WH_KEYBOARD_LL)抢在其它程序(如微信)之前拦截 Alt+A。
    // 系统会先把按键送到本钩子，命中 Alt+A 时触发截图并“吞掉”该键，
    // 因此谁用 RegisterHotKey 抢注 Alt+A 都无效，本工具始终优先。
    // 注意：钩子只判断“是不是 Alt+A”，不记录任何按键内容，非键盘记录器。
    public class TrayApp : Form
    {
        const int WH_KEYBOARD_LL = 13;
        const int WM_KEYDOWN = 0x0100;
        const int WM_SYSKEYDOWN = 0x0104;
        const int VK_A = 0x41;
        const int VK_MENU = 0x12;   // Alt
        const int LLKHF_INJECTED = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }

        delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        NotifyIcon _tray;
        bool _capturing = false;
        IntPtr _hook = IntPtr.Zero;
        HookProc _proc;   // 保持引用，防止被 GC 回收导致钩子失效

        public TrayApp()
        {
            // 窗口本体隐藏，只做消息接收
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.Opacity = 0;
            this.Load += (s, e) => { this.Visible = false; };

            // 用打进 exe 的 Logo 作为托盘/窗口图标
            Icon appIcon;
            try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { appIcon = SystemIcons.Application; }
            this.Icon = appIcon;

            _tray = new NotifyIcon();
            _tray.Icon = appIcon;
            _tray.Text = "AltSnip 截图 (Alt+A)";
            _tray.Visible = true;

            var menu = new ContextMenuStrip();
            menu.Items.Add("截图 (Alt+A)", null, (s, e) => StartCapture());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => { _tray.Visible = false; Application.Exit(); });
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += (s, e) => StartCapture();

            // 安装底层键盘钩子
            _proc = HookCallback;
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero)
            {
                MessageBox.Show("键盘钩子安装失败，Alt+A 可能不生效。\n你仍可双击托盘图标截图。",
                    "截图工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                var kb = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                // 命中 A 键 + 当前 Alt 处于按下状态；忽略程序注入的假按键
                if (kb.vkCode == VK_A && (kb.flags & LLKHF_INJECTED) == 0
                    && (GetAsyncKeyState(VK_MENU) & 0x8000) != 0)
                {
                    // 交给 UI 线程截图，并吞掉这个按键（微信等收不到）
                    if (!_capturing)
                        this.BeginInvoke((Action)StartCapture);
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        void StartCapture()
        {
            if (_capturing) return;
            _capturing = true;
            try
            {
                // 先把当前整个虚拟屏幕（含多显示器）冻结成一张图
                Rectangle vs = SystemInformation.VirtualScreen;
                Bitmap full = new Bitmap(vs.Width, vs.Height);
                using (Graphics g = Graphics.FromImage(full))
                {
                    g.CopyFromScreen(vs.Location, Point.Empty, vs.Size);
                }
                using (var overlay = new OverlayForm(full, vs))
                {
                    overlay.ShowDialog();
                }
                full.Dispose();
            }
            finally { _capturing = false; }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
            base.OnFormClosing(e);
        }

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0 && args[0] == "--preview")
            {
                int hover = args.Length > 2 ? int.Parse(args[2]) : 1;
                OverlayForm.SavePreview(args.Length > 1 ? args[1] : "preview.png", hover);
                return;
            }
            Application.Run(new TrayApp());
        }
    }

    // 全屏选区遮罩：拖框 -> 出现 ✓ / ✗ 按钮 -> 打勾复制到剪贴板
    public class OverlayForm : Form
    {
        Bitmap _full;          // 冻结的屏幕图
        Rectangle _vs;         // 虚拟屏幕范围（含负坐标偏移）
        Point _start;
        Rectangle _sel = Rectangle.Empty;
        bool _dragging = false;
        bool _hasSel = false;

        Rectangle _okBtn = Rectangle.Empty;
        Rectangle _cancelBtn = Rectangle.Empty;
        int _hover = 0;   // 0 无 / 1 对勾 / 2 叉
        const int BTN = 44;

        // 暖琥珀 VI
        static readonly Color C_CARD   = Color.FromArgb(0x1d, 0x15, 0x0b);
        static readonly Color C_GOLD   = Color.FromArgb(0xf4, 0xb7, 0x40);
        static readonly Color C_ORANGE = Color.FromArgb(0xe0, 0x76, 0x2a);
        static readonly Color C_TEXT   = Color.FromArgb(0xf3, 0xec, 0xe0);
        static readonly Color C_TEXT2  = Color.FromArgb(0xb0, 0x9b, 0x80);
        static readonly Color C_DEEP   = Color.FromArgb(0x0e, 0x0a, 0x06);

        public OverlayForm(Bitmap full, Rectangle vs)
        {
            _full = full;
            _vs = vs;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = new Rectangle(0, 0, vs.Width, vs.Height);
            this.Location = vs.Location; // 覆盖整个虚拟屏幕
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.Cursor = Cursors.Cross;
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.BackColor = Color.Black;

            this.MouseDown += OnDown;
            this.MouseMove += OnMove;
            this.MouseUp += OnUp;
            this.KeyDown += OnKey;
        }

        void OnKey(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { this.Close(); }
            else if (e.KeyCode == Keys.Enter && _hasSel) { Confirm(); }
        }

        void OnDown(object s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            // 已有选区时，点到按钮上则处理按钮
            if (_hasSel)
            {
                if (_okBtn.Contains(e.Location)) { Confirm(); return; }
                if (_cancelBtn.Contains(e.Location)) { this.Close(); return; }
            }
            // 否则开始新的拖框
            _dragging = true;
            _hasSel = false;
            _hover = 0;
            this.Cursor = Cursors.Cross;
            _start = e.Location;
            _sel = new Rectangle(e.Location, Size.Empty);
            this.Invalidate();
        }

        void OnMove(object s, MouseEventArgs e)
        {
            if (_dragging)
            {
                int x = Math.Min(_start.X, e.X);
                int y = Math.Min(_start.Y, e.Y);
                int w = Math.Abs(_start.X - e.X);
                int h = Math.Abs(_start.Y - e.Y);
                _sel = new Rectangle(x, y, w, h);
                this.Invalidate();
                return;
            }
            // 选区已定：悬停在按钮上时切换成手型光标并高亮，否则保持十字
            if (_hasSel)
            {
                int h = _okBtn.Contains(e.Location) ? 1
                      : _cancelBtn.Contains(e.Location) ? 2 : 0;
                this.Cursor = (h == 0) ? Cursors.Cross : Cursors.Hand;
                if (h != _hover) { _hover = h; this.Invalidate(); }
            }
        }

        void OnUp(object s, MouseEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            if (_sel.Width >= 3 && _sel.Height >= 3)
            {
                _hasSel = true;
                LayoutButtons();
            }
            else
            {
                _sel = Rectangle.Empty;
                _hasSel = false;
            }
            this.Invalidate();
        }

        void LayoutButtons()
        {
            // 胶囊工具条放选区右下角下方；空间不足则收进框内右下（叉在左、勾在右）
            const int GAP = 8;
            int totalW = BTN * 2;
            int bx = _sel.Right - totalW;
            int by = _sel.Bottom + GAP;
            if (bx < _sel.Left) bx = _sel.Left;
            if (by + BTN > this.Height) by = _sel.Bottom - BTN - GAP; // 放框内
            if (bx + totalW > this.Width) bx = this.Width - totalW - 2;
            if (bx < 0) bx = 2;

            _cancelBtn = new Rectangle(bx, by, BTN, BTN);
            _okBtn = new Rectangle(bx + BTN, by, BTN, BTN);
        }

        void Confirm()
        {
            try
            {
                Rectangle r = _sel;
                r.Intersect(new Rectangle(0, 0, _full.Width, _full.Height));
                if (r.Width <= 0 || r.Height <= 0) { this.Close(); return; }
                using (Bitmap crop = new Bitmap(r.Width, r.Height))
                {
                    using (Graphics g = Graphics.FromImage(crop))
                        g.DrawImage(_full, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
                    Clipboard.SetImage(crop);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("复制失败: " + ex.Message, "截图工具");
            }
            this.Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Render(e.Graphics);
        }

        public void Render(Graphics g)
        {
            // 画冻结屏幕
            g.DrawImageUnscaled(_full, 0, 0);
            // 半透明暗色遮罩铺满
            using (var dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                g.FillRectangle(dim, this.ClientRectangle);

            if (_sel.Width > 0 && _sel.Height > 0)
            {
                // 选区内还原成清晰原图
                g.DrawImage(_full, _sel, _sel, GraphicsUnit.Pixel);
                // 边框（暖金）
                g.SmoothingMode = SmoothingMode.None;
                using (var pen = new Pen(C_GOLD, 1.6f))
                    g.DrawRectangle(pen, _sel);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // 尺寸标注（暖墨底 + 金字）
                string info = _sel.Width + " × " + _sel.Height;
                using (var f = new Font("Segoe UI", 9f, FontStyle.Regular))
                using (var bg = new SolidBrush(Color.FromArgb(235, C_DEEP)))
                using (var fg = new SolidBrush(C_GOLD))
                {
                    SizeF sz = g.MeasureString(info, f);
                    float tx = _sel.Left;
                    float ty = _sel.Top - sz.Height - 6;
                    if (ty < 0) ty = _sel.Top + 4;
                    using (var p = Rounded(new Rectangle((int)tx, (int)ty, (int)sz.Width + 12, (int)sz.Height + 4), 4))
                        g.FillPath(bg, p);
                    g.DrawString(info, f, fg, tx + 6, ty + 2);
                }

                if (_hasSel)
                    DrawButtons(g);
            }
            else if (!_dragging)
            {
                // 提示语（暖墨底 + 暖白字，金色圆点点睛）
                g.SmoothingMode = SmoothingMode.AntiAlias;
                string tip = "拖动框选区域        Enter 复制        Esc 取消";
                using (var f = new Font("Microsoft YaHei", 11f))
                using (var bg = new SolidBrush(Color.FromArgb(210, C_DEEP)))
                using (var fg = new SolidBrush(C_TEXT))
                {
                    SizeF sz = g.MeasureString(tip, f);
                    float tx = (this.Width - sz.Width) / 2;
                    float ty = 44;
                    using (var p = Rounded(new Rectangle((int)(tx - 18), (int)(ty - 9), (int)sz.Width + 36, (int)sz.Height + 18), 10))
                        g.FillPath(bg, p);
                    // 暖金点睛描边
                    using (var pen = new Pen(Color.FromArgb(90, C_ORANGE), 1f))
                    using (var p = Rounded(new Rectangle((int)(tx - 18), (int)(ty - 9), (int)sz.Width + 35, (int)sz.Height + 17), 10))
                        g.DrawPath(pen, p);
                    g.DrawString(tip, f, fg, tx, ty);
                }
            }
        }

        // 微信风悬浮工具条：暖墨胶囊底 + 细描边 + 细笔画图标，悬停高亮
        void DrawButtons(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bar = Rectangle.Union(_cancelBtn, _okBtn);

            // 投影
            using (var sh = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
            using (var p = Rounded(new Rectangle(bar.X, bar.Y + 3, bar.Width, bar.Height), 11))
                g.FillPath(sh, p);
            // 胶囊底（暖墨面板）
            using (var bg = new SolidBrush(C_CARD))
            using (var p = Rounded(bar, 11))
                g.FillPath(bg, p);
            // 悬停高亮（金=确认 / 暖白=取消）
            if (_hover == 1) FillHover(g, _okBtn, C_GOLD);
            else if (_hover == 2) FillHover(g, _cancelBtn, C_TEXT);
            // 橙色结构描边
            using (var pen = new Pen(Color.FromArgb(120, C_ORANGE), 1f))
            using (var p = Rounded(new Rectangle(bar.X, bar.Y, bar.Width - 1, bar.Height - 1), 11))
                g.DrawPath(pen, p);
            // 中缝分隔线
            using (var pen = new Pen(Color.FromArgb(70, C_TEXT2), 1f))
                g.DrawLine(pen, bar.X + BTN, bar.Y + 11, bar.X + BTN, bar.Bottom - 11);

            DrawCross(g, _cancelBtn, _hover == 2 ? C_TEXT : C_TEXT2);
            DrawCheck(g, _okBtn, C_GOLD);
        }

        void FillHover(Graphics g, Rectangle cell, Color c)
        {
            Rectangle r = Rectangle.Inflate(cell, -5, -5);
            using (var b = new SolidBrush(Color.FromArgb(38, c)))
            using (var p = Rounded(r, 8))
                g.FillPath(b, p);
        }

        void DrawCross(Graphics g, Rectangle c, Color col)
        {
            using (var pen = new Pen(col, 2.4f))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                int p = 16;
                g.DrawLine(pen, c.Left + p, c.Top + p, c.Right - p, c.Bottom - p);
                g.DrawLine(pen, c.Right - p, c.Top + p, c.Left + p, c.Bottom - p);
            }
        }

        void DrawCheck(Graphics g, Rectangle c, Color col)
        {
            using (var pen = new Pen(col, 2.6f))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                var p1 = new PointF(c.Left + 14f, c.Top + 23f);
                var p2 = new PointF(c.Left + 20f, c.Top + 29f);
                var p3 = new PointF(c.Left + 31f, c.Top + 16f);
                g.DrawLines(pen, new[] { p1, p2, p3 });
            }
        }

        GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // 隐藏开发用：把真实绘制渲染成 PNG，便于核对视觉。用法 Snip.exe --preview out.png
        internal static void SavePreview(string path, int hover)
        {
            int W = 560, H = 380;
            var bg = new Bitmap(W, H);
            using (var g = Graphics.FromImage(bg))
            {
                using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 0, W, H), Color.FromArgb(48, 76, 120), Color.FromArgb(18, 26, 44), 45f))
                    g.FillRectangle(br, 0, 0, W, H);
                using (var b = new SolidBrush(Color.FromArgb(238, 238, 240)))
                    g.FillRectangle(b, 70, 70, 230, 130);
                using (var b = new SolidBrush(Color.FromArgb(150, 170, 210)))
                    g.FillRectangle(b, 320, 90, 170, 110);
            }
            var f = new OverlayForm(bg, new Rectangle(0, 0, W, H));
            f.Bounds = new Rectangle(0, 0, W, H);
            f._sel = new Rectangle(120, 95, 300, 180);
            f._hasSel = true;
            f.LayoutButtons();
            f._hover = hover;
            using (var outBmp = new Bitmap(W, H))
            {
                using (var g = Graphics.FromImage(outBmp)) f.Render(g);
                outBmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            f.Dispose();
        }
    }
}
