using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RotkAlive.App
{
    sealed class OverlayForm : Form
    {
        const int WS_EX_TOPMOST = 0x8;
        const int WS_EX_TRANSPARENT = 0x20;
        const int WS_EX_TOOLWINDOW = 0x80;
        const int WS_EX_LAYERED = 0x80000;
        const int WS_EX_NOACTIVATE = 0x08000000;
        const int GWL_EXSTYLE = -20;
        const int WM_MOUSEACTIVATE = 0x21;
        const int WM_NCHITTEST = 0x84;
        const int WM_HOTKEY = 0x312;
        const int WM_EXITSIZEMOVE = 0x232;
        const int MA_NOACTIVATE = 3;
        const int HTCAPTION = 2;
        const uint SWP_NOSIZE = 0x1;
        const uint SWP_NOMOVE = 0x2;
        const uint SWP_NOACTIVATE = 0x10;
        const uint MOD_ALT = 0x1;
        const uint MOD_CONTROL = 0x2;
        const uint MOD_NOREPEAT = 0x4000;
        const int ULW_ALPHA = 0x2;
        const byte AC_SRC_OVER = 0;
        const byte AC_SRC_ALPHA = 1;
        const int HotkeyToggle = 1;
        const int HotkeyMove = 2;
        const int HotkeyQuit = 3;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        const int TickMs = 500;
        const int TopmostEveryTicks = 4;
        const int AgeRepaintEveryTicks = 10;
        const int LocateEveryTicks = 60;

        static readonly Color TrayBack = Color.FromArgb(16, 17, 20);
        static readonly Color TrayGold = Color.FromArgb(255, 204, 51);

        readonly Settings settings;
        readonly Timer timer = new Timer();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ToolStripMenuItem showItem;
        readonly ToolStripMenuItem moveItem;

        LogSession session;
        string missingDir;
        int tick;
        bool moveMode;
        bool userHidden;
        bool firstRender = true;

        public OverlayForm(Settings settings)
        {
            this.settings = settings;

            Text = "ROTK alive";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(1, 1);

            ContextMenuStrip menu = new ContextMenuStrip();
            showItem = new ToolStripMenuItem("Hide overlay  (Ctrl+Alt+O)", null, delegate { ToggleVisible(); });
            moveItem = new ToolStripMenuItem("Move mode  (Ctrl+Alt+P)", null, delegate { ToggleMoveMode(); });
            menu.Items.Add(showItem);
            menu.Items.Add(moveItem);
            menu.Items.Add(new ToolStripMenuItem("Open overlay.log", null, delegate { OpenDiagLog(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Exit  (Ctrl+Alt+Q)", null, delegate { Close(); }));
            tray.ContextMenuStrip = menu;
            tray.Icon = MakeTrayIcon();
            tray.Text = "ROTK alive overlay";
            tray.Visible = true;
            tray.DoubleClick += delegate { ToggleVisible(); };

            timer.Interval = TickMs;
            timer.Tick += OnTick;

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED | WS_EX_TRANSPARENT;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Register(HotkeyToggle, 0x4F, "Ctrl+Alt+O");
            Register(HotkeyMove, 0x50, "Ctrl+Alt+P");
            Register(HotkeyQuit, 0x51, "Ctrl+Alt+Q");
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            OnTick(this, EventArgs.Empty);
            timer.Start();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Render();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            UnregisterHotKey(Handle, HotkeyToggle);
            UnregisterHotKey(Handle, HotkeyMove);
            UnregisterHotKey(Handle, HotkeyQuit);
            tray.Visible = false;
            tray.Dispose();
            base.OnFormClosed(e);
        }

        // The layered window is painted only through UpdateLayeredWindow.
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_MOUSEACTIVATE:
                    m.Result = (IntPtr)MA_NOACTIVATE;
                    return;
                case WM_NCHITTEST:
                    if (moveMode)
                    {
                        m.Result = (IntPtr)HTCAPTION;
                        return;
                    }
                    break;
                case WM_EXITSIZEMOVE:
                    if (moveMode) SavePosition();
                    break;
                case WM_HOTKEY:
                    int id = m.WParam.ToInt32();
                    if (id == HotkeyToggle) ToggleVisible();
                    else if (id == HotkeyMove) ToggleMoveMode();
                    else if (id == HotkeyQuit) Close();
                    return;
            }
            base.WndProc(ref m);
        }

        // ---------- polling ----------

        void OnTick(object sender, EventArgs e)
        {
            tick++;
            try
            {
                if (session == null && (tick == 1 || tick % LocateEveryTicks == 0)) Locate();

                bool changed = session != null && session.Poll();
                if (changed || firstRender || tick % AgeRepaintEveryTicks == 0) Render();
                if (tick % TopmostEveryTicks == 0) AssertTopmost();
            }
            catch (Exception ex)
            {
                DiagLog.Error("tick: " + ex);
            }
        }

        void Locate()
        {
            string first;
            string dir = LogLocator.Resolve(settings.LogDir, out first);
            if (dir == null)
            {
                if (missingDir != first) DiagLog.Write("log folder not found: " + first);
                missingDir = first;
                return;
            }
            missingDir = null;
            session = new LogSession(dir);
            DiagLog.Write("reading logs in " + dir);
        }

        void AssertTopmost()
        {
            if (Visible) SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (!IsHandleCreated) return;
            BeginInvoke((MethodInvoker)delegate
            {
                DiagLog.Write("display changed: " + Screen.PrimaryScreen.Bounds);
                Render();
                AssertTopmost();
            });
        }

        // ---------- rendering ----------

        void Render()
        {
            firstRender = false;
            Rectangle screen = Screen.PrimaryScreen.Bounds;

            PanelBuilder.State st = new PanelBuilder.State();
            st.HaveLogDir = session != null;
            st.MissingDir = missingDir;
            st.Errors = DiagLog.ErrorCount;
            if (session != null)
            {
                DateTime nowUtc = DateTime.UtcNow;
                st.LogDir = session.LogDir;
                st.Snapshot = session.Snapshot;
                st.Stale = session.Snapshot.Phase != MatchPhase.NoData &&
                           session.IsStale(nowUtc, TimeSpan.FromMinutes(settings.StaleMinutes));
                st.Age = nowUtc - session.LastWriteUtc;
            }
            PanelModel model = PanelBuilder.Build(st, settings, moveMode);

            using (Bitmap bmp = OverlayRenderer.Render(model, screen.Height, settings))
            {
                Point p = AnchoredLocation(screen, bmp.Width, bmp.Height);
                Rectangle bounds = new Rectangle(p, bmp.Size);
                // Keep WinForms' idea of the bounds in step, otherwise Show() would shrink the window back.
                if (Bounds != bounds) Bounds = bounds;
                Push(bmp, p);
            }
            UpdateTrayText(model);
        }

        void Push(Bitmap bmp, Point location)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;
            try
            {
                hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memDc, hBitmap);
                SIZE size = new SIZE(bmp.Width, bmp.Height);
                POINT source = new POINT(0, 0);
                POINT top = new POINT(location.X, location.Y);
                BLENDFUNCTION blend = new BLENDFUNCTION();
                blend.BlendOp = AC_SRC_OVER;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = AC_SRC_ALPHA;
                if (!UpdateLayeredWindow(Handle, screenDc, ref top, ref size, memDc, ref source, 0, ref blend, ULW_ALPHA))
                    DiagLog.Once("ulw:" + Marshal.GetLastWin32Error(), "UpdateLayeredWindow failed: " + Marshal.GetLastWin32Error());
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    SelectObject(memDc, oldBitmap);
                    DeleteObject(hBitmap);
                }
                DeleteDC(memDc);
            }
        }

        Point AnchoredLocation(Rectangle b, int w, int h)
        {
            int dx = (int)Math.Round(settings.X * b.Width);
            int dy = (int)Math.Round(settings.Y * b.Height);
            int x, y;
            switch (settings.Anchor)
            {
                case Corner.TopRight: x = b.Right - dx - w; y = b.Top + dy; break;
                case Corner.BottomLeft: x = b.Left + dx; y = b.Bottom - dy - h; break;
                case Corner.BottomRight: x = b.Right - dx - w; y = b.Bottom - dy - h; break;
                default: x = b.Left + dx; y = b.Top + dy; break;
            }
            x = Math.Max(b.Left, Math.Min(b.Right - w, x));
            y = Math.Max(b.Top, Math.Min(b.Bottom - h, y));
            return new Point(x, y);
        }

        void SavePosition()
        {
            Rectangle b = Screen.PrimaryScreen.Bounds;
            RECT wr;
            if (!GetWindowRect(Handle, out wr)) return;
            Rectangle w = Rectangle.FromLTRB(wr.Left, wr.Top, wr.Right, wr.Bottom);
            double fx, fy;
            switch (settings.Anchor)
            {
                case Corner.TopRight: fx = b.Right - w.Right; fy = w.Top - b.Top; break;
                case Corner.BottomLeft: fx = w.Left - b.Left; fy = b.Bottom - w.Bottom; break;
                case Corner.BottomRight: fx = b.Right - w.Right; fy = b.Bottom - w.Bottom; break;
                default: fx = w.Left - b.Left; fy = w.Top - b.Top; break;
            }
            settings.X = Math.Max(0, Math.Min(1, fx / b.Width));
            settings.Y = Math.Max(0, Math.Min(1, fy / b.Height));
            settings.Save();
        }

        // ---------- controls ----------

        void ToggleVisible()
        {
            userHidden = !userHidden;
            if (userHidden) Hide();
            else
            {
                Show();
                Render();
                AssertTopmost();
            }
            showItem.Text = userHidden ? "Show overlay  (Ctrl+Alt+O)" : "Hide overlay  (Ctrl+Alt+O)";
        }

        void ToggleMoveMode()
        {
            moveMode = !moveMode;
            int ex = GetWindowLong(Handle, GWL_EXSTYLE);
            ex = moveMode ? (ex & ~WS_EX_TRANSPARENT) : (ex | WS_EX_TRANSPARENT);
            SetWindowLong(Handle, GWL_EXSTYLE, ex);
            moveItem.Checked = moveMode;
            if (!moveMode) SavePosition();
            if (userHidden && moveMode) ToggleVisible();
            Render();
        }

        void OpenDiagLog()
        {
            try
            {
                if (DiagLog.FilePath != null && File.Exists(DiagLog.FilePath))
                    Process.Start("notepad.exe", "\"" + DiagLog.FilePath + "\"");
            }
            catch (Exception ex)
            {
                DiagLog.Write("could not open overlay.log: " + ex.Message);
            }
        }

        void UpdateTrayText(PanelModel model)
        {
            string text = "ROTK: " + model.Title + (model.SubLine != null ? " | " + model.SubLine : "");
            tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        }

        void Register(int id, uint vk, string label)
        {
            if (!RegisterHotKey(Handle, id, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, vk))
                DiagLog.Write("hotkey " + label + " is taken by another program; use the tray icon instead");
        }

        static Icon MakeTrayIcon()
        {
            using (Bitmap bmp = new Bitmap(16, 16))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(TrayBack);
                    using (Brush b = new SolidBrush(TrayGold))
                    {
                        g.FillRectangle(b, 3, 9, 2, 5);
                        g.FillRectangle(b, 7, 5, 2, 9);
                        g.FillRectangle(b, 11, 2, 2, 12);
                    }
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int X, Y;
            public POINT(int x, int y) { X = x; Y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SIZE
        {
            public int Cx, Cy;
            public SIZE(int cx, int cy) { Cx = cx; Cy = cy; }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    }
}
