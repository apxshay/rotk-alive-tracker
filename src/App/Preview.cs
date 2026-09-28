using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;

namespace RotkAlive.App
{
    // TottiGol.exe --preview out.png [screenHeight] [state] [opacity]
    // Renders the panel with built-in sample players, for comparing against the design without a match.
    // state: list (default), stale, empty, waiting, nologs, edit, hint
    // opacity: 0.15 to 1, default from Settings; edit mode is always drawn solid, as on screen.
    static class Preview
    {
        public static int Run(string[] args)
        {
            string outPath = args[1];
            int screenHeight = 1440;
            if (args.Length > 2) int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out screenHeight);
            string state = args.Length > 3 ? args[3] : "list";
            bool edit = state == "edit";
            bool hint = state == "hint";
            if (edit || hint) state = "list";

            Settings settings = Settings.Defaults();
            if (args.Length > 4)
            {
                double o;
                if (double.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out o)) settings.Opacity = o;
                settings.Clamp();
            }
            PanelBuilder.State st = new PanelBuilder.State();
            st.HaveLogDir = state != "nologs";
            st.MissingDir = @"C:\Games\ROTK\Logs";
            st.LogDir = @"C:\Games\ROTK\Logs";
            st.Age = TimeSpan.FromSeconds(12);
            st.Stale = state == "stale";

            MatchSnapshot s = new MatchSnapshot();
            s.Phase = state == "waiting" ? MatchPhase.WaitingForStart : MatchPhase.Started;
            s.HasLastEvent = true;
            s.StartTime = new DateTime(2026, 9, 28, 21, 49, 56);
            s.LastEventTime = new DateTime(2026, 9, 28, 21, 57, 33);
            if (state == "list" || state == "stale")
            {
                Add(s, "Vara", "7.1", 3);
                Add(s, "Boxy_Ace", "7.4", 1);
                Add(s, "Lekid", "6.4", 2);
                Add(s, "Screedy", "5.1", 4);
                Add(s, "Horizon_", "4.3", 0);
                Add(s, "Sous 3x Filtré", "3.2", 1);
                Add(s, "Cyb", "2.3", 1);
                Add(s, "lIlIlIlIlIlIlIlIlIlIClashRoyaleHogrider", "5.5", 0);
                Add(s, "Kayzah", "0.0", 0);
                Add(s, "Ютуб", "4.1", 1);
                for (int i = 0; i < 7; i++) Add(s, "Filler" + i, "3.4", 0);
            }
            s.Alive.Sort(MatchModel.CompareAlive);

            st.Snapshot = s;
            PanelModel model = PanelBuilder.Build(st, settings, edit);
            if (hint) model.Hint = "CTRL+ALT+P EDIT  ·  CTRL+ALT+O HIDE";
            float alpha = edit ? 1f : (float)settings.Opacity;

            using (Bitmap panel = OverlayRenderer.Render(model, screenHeight, settings))
            using (Bitmap canvas = new Bitmap(panel.Width + 80, panel.Height + 80, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(canvas))
            {
                // A dark game-like backdrop so the panel's transparency is visible.
                using (System.Drawing.Drawing2D.LinearGradientBrush bg = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 0, canvas.Width, canvas.Height), Color.FromArgb(46, 30, 52), Color.FromArgb(22, 18, 26), 60f))
                    g.FillRectangle(bg, 0, 0, canvas.Width, canvas.Height);
                ColorMatrix fade = new ColorMatrix();
                fade.Matrix33 = alpha;
                using (ImageAttributes ia = new ImageAttributes())
                {
                    ia.SetColorMatrix(fade);
                    g.DrawImage(panel, new Rectangle(40, 40, panel.Width, panel.Height), 0, 0, panel.Width, panel.Height, GraphicsUnit.Pixel, ia);
                }
                canvas.Save(outPath, ImageFormat.Png);
            }
            Console.WriteLine("wrote " + outPath);
            return 0;
        }

        static void Add(MatchSnapshot s, string name, string rank, int kills)
        {
            AlivePlayer p = new AlivePlayer();
            p.Id = name;
            p.Name = name;
            p.RankText = rank;
            p.RankInfo = RankInfo.Parse(rank);
            p.Kills = kills;
            p.Order = s.Alive.Count;
            s.Alive.Add(p);
        }
    }
}
