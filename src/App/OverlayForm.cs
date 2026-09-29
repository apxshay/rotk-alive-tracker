using System;
using System.Collections.Generic;
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
        const int HTCLIENT = 1;
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
        static readonly TimeSpan HintDuration = TimeSpan.FromSeconds(20);

        static readonly Color TrayBack = Color.FromArgb(16, 17, 20);
        static readonly Color TrayGold = Color.FromArgb(255, 204, 51);

        readonly Settings settings;
        readonly Timer timer = new Timer();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ToolStripMenuItem showItem;
        readonly ToolStripMenuItem moveItem;
        readonly ToolStripMenuItem exitItem;

        LogSession session;
        string missingDir;
        int tick;
        bool moveMode;
        bool userHidden;
        bool firstRender = true;
        bool hotkeyToggleOk, hotkeyMoveOk, hotkeyQuitOk;
        DateTime hintUntilUtc = DateTime.MinValue;
        bool hintShown;
        List<ButtonRect> buttons = new List<ButtonRect>();
        ButtonRect pressed;
        SettingsForm settingsForm;
        readonly LadderService ladder = new LadderService();
        int ladderDrawn;

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
            showItem = new ToolStripMenuItem("", null, delegate { ToggleVisible(); });
            moveItem = new ToolStripMenuItem("", null, delegate { ToggleMoveMode(); });
            exitItem = new ToolStripMenuItem("", null, delegate { Close(); });
            menu.Items.Add(new ToolStripMenuItem("Settings...", null, delegate { OpenSettings(); }));
            menu.Items.Add(moveItem);
            menu.Items.Add(showItem);
            menu.Items.Add(new ToolStripMenuItem("Open overlay.log", null, delegate { OpenDiagLog(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);
            tray.ContextMenuStrip = menu;
            UpdateMenuText();
            tray.Icon = MakeTrayIcon();
            tray.Text = "ROTK alive overlay";
            tray.Visible = true;
            tray.DoubleClick += delegate { ToggleVisible(); };

            timer.Interval = TickMs;
            timer.Tick += OnTick;
            ladder.Start();

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
            hotkeyToggleOk = Register(HotkeyToggle, 0x4F, "Ctrl+Alt+O");
            hotkeyMoveOk = Register(HotkeyMove, 0x50, "Ctrl+Alt+P");
            hotkeyQuitOk = Register(HotkeyQuit, 0x51, "Ctrl+Alt+Q");
            UpdateMenuText();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            StartHint();
            OnTick(this, EventArgs.Empty);
            timer.Start();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Render();
            if (settings.ShowHints)
                tray.ShowBalloonTip(6000, "ROTK alive tracker is running",
                    Key(hotkeyMoveOk, "Ctrl+Alt+P") + " to move it or open settings, " +
                    Key(hotkeyToggleOk, "Ctrl+Alt+O") + " to hide it. Right-click this icon for the menu.",
                    ToolTipIcon.Info);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            ladder.Stop();
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.Close();
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
                        long lp = m.LParam.ToInt64();
                        Point client = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                        m.Result = (IntPtr)(EditToolbar.HitTest(buttons, client) != null ? HTCLIENT : HTCAPTION);
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

        // Only reached in move mode, when the pointer is over a toolbar button (HTCLIENT).
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            pressed = e.Button == MouseButtons.Left ? EditToolbar.HitTest(buttons, e.Location) : null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = EditToolbar.HitTest(buttons, e.Location) != null ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            ButtonRect hit = e.Button == MouseButtons.Left ? EditToolbar.HitTest(buttons, e.Location) : null;
            ButtonRect down = pressed;
            pressed = null;
            if (!moveMode || hit == null || down == null || hit.Id != down.Id) return;
            BeginInvoke((MethodInvoker)delegate { OnToolbar(hit.Id); });
        }

        void OnToolbar(ToolbarButton id)
        {
            if (!moveMode) return;
            switch (id)
            {
                case ToolbarButton.Settings:
                    ToggleMoveMode();
                    OpenSettings();
                    break;
                case ToolbarButton.Hide:
                    ToggleMoveMode();
                    if (!userHidden) ToggleVisible();
                    break;
                case ToolbarButton.Done:
                    ToggleMoveMode();
                    break;
            }
        }

        // ---------- polling ----------

        void OnTick(object sender, EventArgs e)
        {
            tick++;
            try
            {
                if (session == null && (tick == 1 || tick % LocateEveryTicks == 0)) Locate();

                bool changed = session != null && session.Poll();
                if (session != null)
                    ladder.Observe(session.Snapshot, settings.MaxRows, settings.LadderRegion, settings.LadderMode);
                bool hintExpired = hintShown && !HintActive();
                bool ladderChanged = ladder.Generation != ladderDrawn;
                if (changed || firstRender || hintExpired || ladderChanged || tick % AgeRepaintEveryTicks == 0) Render();
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
            int seenLadder = ladder.Generation;
            PanelModel model = PanelBuilder.Build(st, settings, moveMode, ladder.Cache);
            ladderDrawn = seenLadder;
            hintShown = !moveMode && HintActive();
            if (hintShown) model.Hint = HintText();

            using (Bitmap bmp = OverlayRenderer.Render(model, screen.Height, settings))
            {
                buttons = model.Buttons;
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
                // Move mode is always drawn solid so the toolbar stays readable at low opacity.
                blend.SourceConstantAlpha = moveMode ? (byte)255 : (byte)Math.Round(255 * settings.Opacity);
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
                StartHint();
                Show();
                Render();
                AssertTopmost();
            }
            UpdateMenuText();
        }

        void ToggleMoveMode()
        {
            moveMode = !moveMode;
            int ex = GetWindowLong(Handle, GWL_EXSTYLE);
            ex = moveMode ? (ex & ~WS_EX_TRANSPARENT) : (ex | WS_EX_TRANSPARENT);
            SetWindowLong(Handle, GWL_EXSTYLE, ex);
            moveItem.Checked = moveMode;
            pressed = null;
            if (!moveMode)
            {
                Cursor = Cursors.Default;
                SavePosition();
            }
            if (userHidden && moveMode) ToggleVisible();
            Render();
        }

        void OpenSettings()
        {
            if (settingsForm != null && !settingsForm.IsDisposed)
            {
                if (settingsForm.WindowState == FormWindowState.Minimized) settingsForm.WindowState = FormWindowState.Normal;
                settingsForm.Activate();
                return;
            }
            if (userHidden) ToggleVisible();
            settingsForm = new SettingsForm(settings, ShortcutLines(), OnSettingsChanged, OnResetPosition);
            settingsForm.FormClosed += delegate
            {
                settings.Save();
                settingsForm = null;
            };
            settingsForm.Show();
        }

        void OnSettingsChanged(bool hintsTurnedOn)
        {
            if (hintsTurnedOn) StartHint();
            Render();
        }

        void OnResetPosition()
        {
            settings.ResetPosition();
            Render();
            AssertTopmost();
        }

        void StartHint()
        {
            hintUntilUtc = settings.ShowHints ? DateTime.UtcNow + HintDuration : DateTime.MinValue;
        }

        bool HintActive()
        {
            return settings.ShowHints && DateTime.UtcNow < hintUntilUtc;
        }

        string HintText()
        {
            return (hotkeyMoveOk ? "CTRL+ALT+P EDIT" : "EDIT: TRAY ONLY") + "  ·  " +
                   (hotkeyToggleOk ? "CTRL+ALT+O HIDE" : "HIDE: TRAY ONLY");
        }

        string[] ShortcutLines()
        {
            return new string[]
            {
                Key(hotkeyMoveOk, "Ctrl+Alt+P") + "    edit mode: move the panel, open settings, hide",
                Key(hotkeyToggleOk, "Ctrl+Alt+O") + "    hide or show the panel",
                Key(hotkeyQuitOk, "Ctrl+Alt+Q") + "    quit the tracker",
            };
        }

        static string Key(bool ok, string key)
        {
            return ok ? key : key + " (taken, use the tray icon)";
        }

        void UpdateMenuText()
        {
            showItem.Text = (userHidden ? "Show" : "Hide") + "  (" + (hotkeyToggleOk ? "Ctrl+Alt+O" : "tray only") + ")";
            moveItem.Text = "Edit / move  (" + (hotkeyMoveOk ? "Ctrl+Alt+P" : "tray only") + ")";
            exitItem.Text = "Exit  (" + (hotkeyQuitOk ? "Ctrl+Alt+Q" : "tray only") + ")";
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

        bool Register(int id, uint vk, string label)
        {
            if (RegisterHotKey(Handle, id, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, vk)) return true;
            DiagLog.Write("hotkey " + label + " is taken by another program; use the tray icon instead");
            return false;
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
