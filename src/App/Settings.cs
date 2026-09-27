using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace RotkAlive.App
{
    public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

    // overlay.ini next to the exe. Positions are fractions of the screen and sizes are fractions of
    // the screen height, so nothing depends on the game's resolution.
    public sealed class Settings
    {
        public string LogDir = "";
        public Corner Anchor = Corner.TopLeft;
        public double X = 0.006;
        public double Y = 0.25;
        public double FontScale = 0.011;
        public double MaxWidthScale = 0.16;
        public int MaxRows = 15;
        public double HighRank = 7.0;
        public double MidRank = 6.0;
        public double StaleMinutes = 5;
        public double Opacity = 0.8;

        string path;

        public static Settings Load(string path)
        {
            Settings s = new Settings();
            s.path = path;
            if (!File.Exists(path))
            {
                s.Save();
                return s;
            }

            try
            {
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';' || line[0] == '[') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    s.Apply(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim());
                }
            }
            catch (Exception ex)
            {
                DiagLog.Write("could not read " + path + ", using defaults: " + ex.Message);
            }
            s.Clamp();
            return s;
        }

        void Apply(string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "logdir": LogDir = value; break;
                case "anchor":
                    Corner c;
                    if (Enum.TryParse(value, true, out c)) Anchor = c;
                    break;
                case "x": X = Num(value, X); break;
                case "y": Y = Num(value, Y); break;
                case "fontscale": FontScale = Num(value, FontScale); break;
                case "maxwidthscale": MaxWidthScale = Num(value, MaxWidthScale); break;
                case "maxrows": MaxRows = (int)Num(value, MaxRows); break;
                case "highrank": HighRank = Num(value, HighRank); break;
                case "midrank": MidRank = Num(value, MidRank); break;
                case "staleminutes": StaleMinutes = Num(value, StaleMinutes); break;
                case "opacity": Opacity = Num(value, Opacity); break;
            }
        }

        void Clamp()
        {
            X = Math.Max(0, Math.Min(1, X));
            Y = Math.Max(0, Math.Min(1, Y));
            FontScale = Math.Max(0.005, Math.Min(0.05, FontScale));
            MaxWidthScale = Math.Max(0.05, Math.Min(0.6, MaxWidthScale));
            MaxRows = Math.Max(1, Math.Min(60, MaxRows));
            StaleMinutes = Math.Max(0.5, StaleMinutes);
            // Click-through needs a layered window; WinForms only makes it layered below 1.0.
            Opacity = Math.Max(0.3, Math.Min(0.99, Opacity));
        }

        static double Num(string s, double fallback)
        {
            double v;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        static string F(double v)
        {
            return v.ToString("0.####", CultureInfo.InvariantCulture);
        }

        public void Save()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# ROTK alive overlay settings. Numbers use a dot.");
            sb.AppendLine("# Empty LogDir = read installation.root from %APPDATA%\\ROTK Launcher\\config.v1.json, else C:\\Games\\ROTK.");
            sb.AppendLine("LogDir=" + LogDir);
            sb.AppendLine("# Anchor: TopLeft, TopRight, BottomLeft, BottomRight. X and Y are fractions of the screen from that corner.");
            sb.AppendLine("Anchor=" + Anchor);
            sb.AppendLine("X=" + F(X));
            sb.AppendLine("Y=" + F(Y));
            sb.AppendLine("# Sizes are fractions of the screen height.");
            sb.AppendLine("FontScale=" + F(FontScale));
            sb.AppendLine("MaxWidthScale=" + F(MaxWidthScale));
            sb.AppendLine("MaxRows=" + MaxRows.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("HighRank=" + F(HighRank));
            sb.AppendLine("MidRank=" + F(MidRank));
            sb.AppendLine("StaleMinutes=" + F(StaleMinutes));
            sb.AppendLine("Opacity=" + F(Opacity));
            try
            {
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                DiagLog.Write("could not write " + path + ": " + ex.Message);
            }
        }
    }
}
