using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
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
        const int HotkeyToggle = 1;
        const int HotkeyMove = 2;
        const int HotkeyQuit = 3;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        const int TickMs = 500;
        const int TopmostEveryTicks = 4;
        const int AgeRepaintEveryTicks = 10;
        const int LocateEveryTicks = 60;

        static readonly Color Back = Color.FromArgb(16, 17, 20);
        static readonly Color TextMain = Color.FromArgb(232, 232, 232);
        static readonly Color TextDim = Color.FromArgb(150, 150, 150);
        static readonly Color TextStale = Color.FromArgb(120, 120, 120);
        static readonly Color Gold = Color.FromArgb(255, 204, 51);
        static readonly Color Orange = Color.FromArgb(255, 145, 50);
        static readonly Color Zero = Color.FromArgb(135, 135, 135);
        static readonly Color Warn = Color.FromArgb(255, 110, 90);
        static readonly Color MoveBorder = Color.FromArgb(255, 204, 51);

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

        float fontPx;
        Font nameFont, nameBold, rankFont, rankBold, headFont, smallFont;
        int pad, rowH, rankColW;

        sealed class Row
        {
            public string Rank;
            public string Text;
            public Color Color;
            public bool Bold;
            public bool Small;
        }

        readonly List<Row> rows = new List<Row>();

        public OverlayForm(Settings settings)
        {
            this.settings = settings;

            Text = "ROTK alive";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Back;
            Opacity = settings.Opacity;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

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

        // ---------- layout ----------

        void Render()
        {
            firstRender = false;
            Rectangle screen = Screen.PrimaryScreen.Bounds;
            EnsureFonts(screen.Height);
            BuildRows();

            int maxW = Math.Max((int)(fontPx * 8), (int)(screen.Height * settings.MaxWidthScale));
            int contentW = 0;
            foreach (Row r in rows)
            {
                int w;
                if (r.Rank != null)
                    w = rankColW + TextRenderer.MeasureText(r.Text, r.Bold ? nameBold : nameFont).Width;
                else
                    w = TextRenderer.MeasureText(r.Text, RowFont(r)).Width;
                if (w > contentW) contentW = w;
            }
            int width = Math.Min(maxW, Math.Max((int)(fontPx * 8), contentW + pad * 2));
            int height = pad * 2 + rows.Count * rowH;

            Point p = AnchoredLocation(screen, width, height);
            Rectangle bounds = new Rectangle(p, new Size(width, height));
            if (Bounds != bounds) Bounds = bounds;

            UpdateTrayText();
            Invalidate();
        }

        void EnsureFonts(int screenHeight)
        {
            float px = (float)Math.Max(9.0, Math.Round(screenHeight * settings.FontScale));
            if (nameFont != null && Math.Abs(px - fontPx) < 0.5f) return;

            fontPx = px;
            DisposeFonts();
            nameFont = new Font("Segoe UI", px, FontStyle.Regular, GraphicsUnit.Pixel);
            nameBold = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel);
            rankFont = new Font("Consolas", px, FontStyle.Regular, GraphicsUnit.Pixel);
            rankBold = new Font("Consolas", px, FontStyle.Bold, GraphicsUnit.Pixel);
            headFont = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel);
            smallFont = new Font("Segoe UI", px * 0.85f, FontStyle.Regular, GraphicsUnit.Pixel);
            pad = (int)Math.Ceiling(px * 0.4);
            rowH = (int)Math.Ceiling(px * 1.3);
            rankColW = TextRenderer.MeasureText("0.0", rankBold).Width + (int)Math.Ceiling(px * 0.3);
        }

        void DisposeFonts()
        {
            foreach (Font f in new Font[] { nameFont, nameBold, rankFont, rankBold, headFont, smallFont })
                if (f != null) f.Dispose();
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
            Rectangle w = Bounds;
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

        // ---------- content ----------

        void BuildRows()
        {
            rows.Clear();

            if (session == null)
            {
                Add(null, "Log folder not found", Warn, true, false);
                Add(null, missingDir ?? LogLocator.DefaultRoot + "\\Logs", TextDim, false, true);
                return;
            }

            MatchSnapshot s = session.Snapshot;
            DateTime nowUtc = DateTime.UtcNow;
            bool stale = s.Phase != MatchPhase.NoData &&
                         session.IsStale(nowUtc, TimeSpan.FromMinutes(settings.StaleMinutes));

            string extras = "";
            if (s.Unparsed > 0) extras += " | " + s.Unparsed + " unparsed";
            if (DiagLog.ErrorCount > 0) extras += " | !";

            if (s.Phase == MatchPhase.NoData)
            {
                Add(null, "Waiting for game logs", TextMain, true, false);
                Add(null, session.LogDir + extras, TextDim, false, true);
                return;
            }

            string age = "last event " + Age(nowUtc - session.LastWriteUtc) + " ago" + extras;
            string lastClock = s.LastEventTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

            if (s.Phase == MatchPhase.WaitingForStart)
            {
                Add(null, stale ? "STALE | last event " + lastClock : "Waiting for match start", stale ? TextStale : TextMain, true, false);
                Add(null, stale ? "waiting for match start" + extras : age, TextDim, false, true);
                return;
            }

            string startClock = s.StartTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            if (s.Alive.Count == 0)
            {
                Add(null, stale ? "STALE | last event " + lastClock : "Match started " + startClock, stale ? TextStale : TextMain, true, false);
                Add(null, "nobody revealed yet | " + (stale ? "start " + startClock + extras : age), TextDim, false, true);
                return;
            }

            AlivePlayer top = s.Top;
            string summary = "ALIVE " + s.Alive.Count + " | TOP " + top.RankText + " " + top.Name;
            if (stale)
            {
                Add(null, "STALE | last event " + lastClock, TextStale, true, false);
                Add(null, summary + extras, TextStale, false, true);
            }
            else
            {
                Add(null, summary, top.Rank >= settings.HighRank ? Gold : TextMain, true, false);
                Add(null, age, TextDim, false, true);
            }

            int shown = Math.Min(s.Alive.Count, settings.MaxRows);
            for (int i = 0; i < shown; i++)
            {
                AlivePlayer p = s.Alive[i];
                Color c;
                bool bold = false;
                if (stale) c = TextStale;
                else if (p.Rank >= settings.HighRank) { c = Gold; bold = true; }
                else if (p.Rank >= settings.MidRank) c = Orange;
                else if (p.Rank == 0.0) c = Zero;
                else c = TextMain;
                Add(p.RankText, p.Name, c, bold, false);
            }

            int hidden = s.Alive.Count - shown;
            if (hidden > 0)
                Add(null, "+" + hidden + " more (<= " + s.Alive[shown].RankText + ")", TextDim, false, true);
        }

        void Add(string rank, string text, Color color, bool bold, bool small)
        {
            Row r = new Row();
            r.Rank = rank;
            r.Text = text;
            r.Color = color;
            r.Bold = bold;
            r.Small = small;
            rows.Add(r);
        }

        Font RowFont(Row r)
        {
            if (r.Small) return smallFont;
            if (r.Rank == null && r.Bold) return headFont;
            return r.Bold ? nameBold : nameFont;
        }

        static string Age(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            if (t.TotalSeconds < 60) return ((int)t.TotalSeconds) + "s";
            if (t.TotalMinutes < 60) return ((int)t.TotalMinutes) + "m";
            return ((int)t.TotalHours) + "h";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Back);
            if (nameFont == null) return;

            const TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine |
                                          TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter |
                                          TextFormatFlags.NoPadding;
            int y = pad;
            int innerW = ClientSize.Width - pad * 2;
            foreach (Row r in rows)
            {
                if (r.Rank != null)
                {
                    TextRenderer.DrawText(g, r.Rank, r.Bold ? rankBold : rankFont,
                        new Rectangle(pad, y, rankColW, rowH), r.Color, flags);
                    TextRenderer.DrawText(g, r.Text, r.Bold ? nameBold : nameFont,
                        new Rectangle(pad + rankColW, y, innerW - rankColW, rowH), r.Color, flags);
                }
                else
                {
                    TextRenderer.DrawText(g, r.Text, RowFont(r), new Rectangle(pad, y, innerW, rowH), r.Color, flags);
                }
                y += rowH;
            }

            if (moveMode)
            {
                using (Pen pen = new Pen(MoveBorder, 2))
                    g.DrawRectangle(pen, 1, 1, ClientSize.Width - 2, ClientSize.Height - 2);
            }
        }

        // ---------- controls ----------

        void ToggleVisible()
        {
            userHidden = !userHidden;
            if (userHidden) Hide();
            else
            {
                Show();
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
            Invalidate();
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

        void UpdateTrayText()
        {
            string text = rows.Count > 0 ? "ROTK alive: " + rows[0].Text : "ROTK alive overlay";
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
                    g.Clear(Back);
                    using (Brush b = new SolidBrush(Gold))
                    {
                        g.FillRectangle(b, 3, 9, 2, 5);
                        g.FillRectangle(b, 7, 5, 2, 9);
                        g.FillRectangle(b, 11, 2, 2, 12);
                    }
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

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
