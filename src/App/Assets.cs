using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace RotkAlive.App
{
    // Rank emblems and the Oswald font are embedded in the exe (see build.cmd /resource).
    static class Assets
    {
        public const string FallbackFamily = "Segoe UI";

        static readonly Dictionary<string, Bitmap> Icons = new Dictionary<string, Bitmap>();
        static PrivateFontCollection fonts;
        static IntPtr fontMemory = IntPtr.Zero;
        static FontFamily oswald;
        static bool loaded;

        public static FontFamily Oswald
        {
            get { Load(); return oswald; }
        }

        public static Bitmap Icon(string key)
        {
            Load();
            Bitmap b;
            return Icons.TryGetValue(key, out b) ? b : null;
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            Assembly asm = Assembly.GetExecutingAssembly();

            foreach (string key in new string[] { "placement", "bronze", "silver", "gold", "platinum", "diamond", "master", "royalty", "royalty-one" })
            {
                try
                {
                    using (Stream s = asm.GetManifestResourceStream("rank." + key + ".png"))
                    {
                        if (s == null) { DiagLog.Write("missing embedded icon " + key); continue; }
                        using (Bitmap raw = new Bitmap(s))
                            Icons[key] = new Bitmap(raw);
                    }
                }
                catch (Exception ex)
                {
                    DiagLog.Write("could not load icon " + key + ": " + ex.Message);
                }
            }

            try
            {
                using (Stream s = asm.GetManifestResourceStream("font.Oswald-Bold.ttf"))
                {
                    if (s != null)
                    {
                        byte[] data = new byte[s.Length];
                        s.Read(data, 0, data.Length);
                        // GDI+ reads the font from this memory for the life of the process.
                        fontMemory = Marshal.AllocCoTaskMem(data.Length);
                        Marshal.Copy(data, 0, fontMemory, data.Length);
                        fonts = new PrivateFontCollection();
                        fonts.AddMemoryFont(fontMemory, data.Length);
                        if (fonts.Families.Length > 0) oswald = fonts.Families[0];
                    }
                }
            }
            catch (Exception ex)
            {
                DiagLog.Write("could not load Oswald font, using " + FallbackFamily + ": " + ex.Message);
            }
            if (oswald == null) oswald = new FontFamily(FallbackFamily);
        }

        // Oswald covers Latin, Latin Extended and Cyrillic; anything else is drawn with Segoe UI.
        public static bool OswaldCovers(string text)
        {
            foreach (char c in text)
            {
                if (c < 0x0250) continue;
                if (c >= 0x0400 && c <= 0x04FF) continue;
                if (c >= 0x2000 && c <= 0x206F) continue;
                return false;
            }
            return true;
        }
    }
}
