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
        public const Corner DefaultAnchor = Corner.TopLeft;
        public const double DefaultX = 0.006;
        public const double DefaultY = 0.25;
        public const double DefaultOpacity = 0.70;
        public const double MinOpacity = 0.15;

        public string LogDir = "";
        public Corner Anchor = DefaultAnchor;
        public double X = DefaultX;
        public double Y = DefaultY;
        public double FontScale = 0.011;
        public double PanelWidthScale = 0.20;
        public int MaxRows = 15;
        public bool UppercaseNames = true;
        public double StaleMinutes = 5;
        public double Opacity = DefaultOpacity;
        public bool ShowHints = true;
        public double PanelOpacity = 0.88;

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
            // Rewrite so keys from older versions are dropped and new ones appear with defaults.
            s.Save();
            return s;
        }

        public static Settings Defaults()
        {
            return new Settings();
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
                case "panelwidthscale": PanelWidthScale = Num(value, PanelWidthScale); break;
                case "maxrows": MaxRows = (int)Num(value, MaxRows); break;
                case "uppercasenames": UppercaseNames = Num(value, UppercaseNames ? 1 : 0) != 0; break;
                case "staleminutes": StaleMinutes = Num(value, StaleMinutes); break;
                case "opacity": Opacity = Num(value, Opacity); break;
                case "showhints": ShowHints = Num(value, ShowHints ? 1 : 0) != 0; break;
                case "panelopacity": PanelOpacity = Num(value, PanelOpacity); break;
            }
        }

        public void ResetPosition()
        {
            Anchor = DefaultAnchor;
            X = DefaultX;
            Y = DefaultY;
        }

        public void Clamp()
        {
            Opacity = Math.Max(MinOpacity, Math.Min(1.0, Opacity));
            X = Math.Max(0, Math.Min(1, X));
            Y = Math.Max(0, Math.Min(1, Y));
            FontScale = Math.Max(0.005, Math.Min(0.05, FontScale));
            PanelWidthScale = Math.Max(0.08, Math.Min(0.8, PanelWidthScale));
            MaxRows = Math.Max(1, Math.Min(60, MaxRows));
            StaleMinutes = Math.Max(0.5, StaleMinutes);
            PanelOpacity = Math.Max(0.1, Math.Min(1.0, PanelOpacity));
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
            sb.AppendLine("PanelWidthScale=" + F(PanelWidthScale));
            sb.AppendLine("MaxRows=" + MaxRows.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("# 1 = names in capitals like the game UI, 0 = as written in the kill feed.");
            sb.AppendLine("UppercaseNames=" + (UppercaseNames ? "1" : "0"));
            sb.AppendLine("StaleMinutes=" + F(StaleMinutes));
            sb.AppendLine("# Whole panel, 0.15 to 1. Set it from Settings in the tray menu or in edit mode (Ctrl+Alt+P).");
            sb.AppendLine("Opacity=" + F(Opacity));
            sb.AppendLine("# 1 = show the shortcut hint on the panel for a few seconds after start.");
            sb.AppendLine("ShowHints=" + (ShowHints ? "1" : "0"));
            sb.AppendLine("# Background only, on top of Opacity.");
            sb.AppendLine("PanelOpacity=" + F(PanelOpacity));
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
