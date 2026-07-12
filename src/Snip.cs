using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SnipTool
{
    // 托盘常驻程序：注册全局热键 Alt + A，触发截图
    public class TrayApp : Form
    {
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        const int WM_HOTKEY = 0x0312;
        const int HOTKEY_ID = 0xA11A;
        const uint MOD_ALT = 0x0001;
        const uint MOD_NOREPEAT = 0x4000;
        const uint VK_A = 0x41;

        NotifyIcon _tray;
        bool _capturing = false;

        public TrayApp()
        {
            // 窗口本体隐藏，只做消息接收
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.Opacity = 0;
            this.Load += (s, e) => { this.Visible = false; };

            _tray = new NotifyIcon();
            _tray.Icon = SystemIcons.Application;
            _tray.Text = "截图工具 (Alt+A)";
            _tray.Visible = true;

            var menu = new ContextMenuStrip();
            menu.Items.Add("截图 (Alt+A)", null, (s, e) => StartCapture());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => { _tray.Visible = false; Application.Exit(); });
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += (s, e) => StartCapture();

            if (!RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT | MOD_NOREPEAT, VK_A))
            {
                MessageBox.Show("热键 Alt+A 注册失败，可能已被其它程序占用。\n你仍可双击托盘图标截图。",
                    "截图工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && (int)m.WParam == HOTKEY_ID)
            {
                StartCapture();
            }
            base.WndProc(ref m);
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
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            base.OnFormClosing(e);
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
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
        const int BTN = 34;
        const int GAP = 8;

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
            _start = e.Location;
            _sel = new Rectangle(e.Location, Size.Empty);
            this.Invalidate();
        }

        void OnMove(object s, MouseEventArgs e)
        {
            if (!_dragging) return;
            int x = Math.Min(_start.X, e.X);
            int y = Math.Min(_start.Y, e.Y);
            int w = Math.Abs(_start.X - e.X);
            int h = Math.Abs(_start.Y - e.Y);
            _sel = new Rectangle(x, y, w, h);
            this.Invalidate();
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
            // 按钮放在选区右下角下方；若空间不足则放到框内右下
            int totalW = BTN * 2 + GAP;
            int bx = _sel.Right - totalW;
            int by = _sel.Bottom + GAP;
            if (bx < _sel.Left) bx = _sel.Left;
            if (by + BTN > this.Height) by = _sel.Bottom - BTN - GAP; // 放框内
            if (bx + totalW > this.Width) bx = this.Width - totalW - 2;
            if (bx < 0) bx = 2;

            _cancelBtn = new Rectangle(bx, by, BTN, BTN);
            _okBtn = new Rectangle(bx + BTN + GAP, by, BTN, BTN);
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
            Graphics g = e.Graphics;
            // 画冻结屏幕
            g.DrawImageUnscaled(_full, 0, 0);
            // 半透明暗色遮罩铺满
            using (var dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                g.FillRectangle(dim, this.ClientRectangle);

            if (_sel.Width > 0 && _sel.Height > 0)
            {
                // 选区内还原成清晰原图
                g.DrawImage(_full, _sel, _sel, GraphicsUnit.Pixel);
                // 边框
                using (var pen = new Pen(Color.FromArgb(0, 174, 255), 2))
                    g.DrawRectangle(pen, _sel);
                // 尺寸标注
                string info = _sel.Width + " x " + _sel.Height;
                using (var f = new Font("Segoe UI", 9))
                using (var bg = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                using (var fg = new SolidBrush(Color.White))
                {
                    SizeF sz = g.MeasureString(info, f);
                    float tx = _sel.Left;
                    float ty = _sel.Top - sz.Height - 4;
                    if (ty < 0) ty = _sel.Top + 4;
                    g.FillRectangle(bg, tx, ty, sz.Width + 8, sz.Height + 2);
                    g.DrawString(info, f, fg, tx + 4, ty + 1);
                }

                if (_hasSel)
                    DrawButtons(g);
            }
            else if (!_dragging)
            {
                // 提示语
                string tip = "拖动鼠标框选区域   ·   Enter/✓ 复制   ·   Esc 取消";
                using (var f = new Font("Segoe UI", 12))
                using (var bg = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
                using (var fg = new SolidBrush(Color.White))
                {
                    SizeF sz = g.MeasureString(tip, f);
                    float tx = (this.Width - sz.Width) / 2;
                    float ty = 40;
                    g.FillRectangle(bg, tx - 12, ty - 6, sz.Width + 24, sz.Height + 12);
                    g.DrawString(tip, f, fg, tx, ty);
                }
            }
        }

        void DrawButtons(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // 取消 ✗（红）
            DrawButton(g, _cancelBtn, Color.FromArgb(230, 76, 76));
            using (var pen = new Pen(Color.White, 3))
            {
                int p = 10;
                g.DrawLine(pen, _cancelBtn.Left + p, _cancelBtn.Top + p, _cancelBtn.Right - p, _cancelBtn.Bottom - p);
                g.DrawLine(pen, _cancelBtn.Right - p, _cancelBtn.Top + p, _cancelBtn.Left + p, _cancelBtn.Bottom - p);
            }
            // 确认 ✓（绿）
            DrawButton(g, _okBtn, Color.FromArgb(46, 190, 110));
            using (var pen = new Pen(Color.White, 3))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                var p1 = new Point(_okBtn.Left + 9, _okBtn.Top + 18);
                var p2 = new Point(_okBtn.Left + 15, _okBtn.Top + 24);
                var p3 = new Point(_okBtn.Right - 8, _okBtn.Top + 10);
                g.DrawLines(pen, new[] { p1, p2, p3 });
            }
        }

        void DrawButton(Graphics g, Rectangle r, Color c)
        {
            using (var b = new SolidBrush(c))
            using (var path = Rounded(r, 8))
                g.FillPath(b, path);
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
    }
}
