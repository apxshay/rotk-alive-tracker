using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace RotkAlive.Tools
{
    // Read-only extractor for UI textures in the game's .pack2 archives.
    //   IconExtract dump   <outDir> [gameRoot]            every UI texture as PNG plus numbered contact sheets
    //   IconExtract export <listFile> <outDir> [gameRoot] chosen textures by entry hash, transparent border trimmed
    static class IconExtract
    {
        const string DefaultGameRoot = @"C:\Games\ROTK";
        static readonly string[] UiPacks = { "ui_x64_0.pack2", "ui_x64_1.pack2", "ui_x64_2.pack2", "ui_x64_3.pack2" };

        sealed class Entry
        {
            public string Pack;
            public string PackPath;
            public ulong Hash;
            public ulong Offset;
            public ulong Length;
        }

        static int Main(string[] args)
        {
            try
            {
                if (args.Length >= 2 && args[0] == "dump")
                    return Dump(args[1], args.Length > 2 ? args[2] : DefaultGameRoot);
                if (args.Length >= 3 && args[0] == "export")
                    return Export(args[1], args[2], args.Length > 3 ? args[3] : DefaultGameRoot);
                if (args.Length >= 2 && args[0] == "placement")
                    return Placement(args[1]);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
            Console.WriteLine("usage: IconExtract dump <outDir> [gameRoot]");
            Console.WriteLine("       IconExtract export <listFile> <outDir> [gameRoot]");
            Console.WriteLine("       IconExtract placement <out.png>");
            return 2;
        }

        // The game has no Placement emblem; draw a plain grey medallion matching the tier emblems' size.
        static int Placement(string outPath)
        {
            const int size = 122;
            using (Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                Rectangle outer = new Rectangle(3, 3, size - 7, size - 7);
                using (System.Drawing.Drawing2D.LinearGradientBrush ring = new System.Drawing.Drawing2D.LinearGradientBrush(
                    outer, Color.FromArgb(150, 150, 154), Color.FromArgb(58, 58, 62), 90f))
                    g.FillEllipse(ring, outer);
                Rectangle inner = Rectangle.Inflate(outer, -12, -12);
                using (System.Drawing.Drawing2D.LinearGradientBrush face = new System.Drawing.Drawing2D.LinearGradientBrush(
                    inner, Color.FromArgb(70, 70, 74), Color.FromArgb(34, 34, 38), 90f))
                    g.FillEllipse(face, inner);
                using (Pen edge = new Pen(Color.FromArgb(200, 20, 20, 22), 2f))
                {
                    g.DrawEllipse(edge, outer);
                    g.DrawEllipse(edge, inner);
                }
                using (Pen shine = new Pen(Color.FromArgb(90, 255, 255, 255), 2f))
                    g.DrawArc(shine, Rectangle.Inflate(outer, -4, -4), 200, 110);
                bmp.Save(outPath, ImageFormat.Png);
            }
            Console.WriteLine("wrote " + outPath);
            return 0;
        }

        // ---------- commands ----------

        static int Dump(string outDir, string gameRoot)
        {
            Directory.CreateDirectory(outDir);
            string texDir = Path.Combine(outDir, "tex");
            Directory.CreateDirectory(texDir);

            List<string> index = new List<string>();
            List<Bitmap> thumbs = new List<Bitmap>();
            int n = 0;
            foreach (Entry e in Entries(gameRoot))
            {
                byte[] data = Read(e);
                if (!IsDds(data)) continue;
                int w = BitConverter.ToInt32(data, 16), h = BitConverter.ToInt32(data, 12);
                if (w < 24 || h < 24 || w > 1024 || h > 1024) continue;
                Bitmap bmp = DecodeDds(data);
                if (bmp == null) continue;

                string name = string.Format(CultureInfo.InvariantCulture, "{0:D4}_{1:X16}.png", n, e.Hash);
                bmp.Save(Path.Combine(texDir, name), ImageFormat.Png);
                index.Add(string.Format(CultureInfo.InvariantCulture, "{0}\t{1:X16}\t{2}\t{3}x{4}", n, e.Hash, e.Pack, w, h));
                thumbs.Add(bmp);
                n++;
                if (thumbs.Count == 100)
                {
                    WriteSheet(thumbs, n - 100, Path.Combine(outDir, string.Format(CultureInfo.InvariantCulture, "sheet_{0:D2}.png", (n - 1) / 100)));
                    foreach (Bitmap b in thumbs) b.Dispose();
                    thumbs.Clear();
                }
            }
            if (thumbs.Count > 0)
            {
                WriteSheet(thumbs, n - thumbs.Count, Path.Combine(outDir, string.Format(CultureInfo.InvariantCulture, "sheet_{0:D2}.png", (n - 1) / 100)));
                foreach (Bitmap b in thumbs) b.Dispose();
            }
            File.WriteAllLines(Path.Combine(outDir, "index.tsv"), index.ToArray());
            Console.WriteLine("dumped " + n + " textures to " + outDir);
            return 0;
        }

        static int Export(string listFile, string outDir, string gameRoot)
        {
            Dictionary<ulong, string> wanted = new Dictionary<ulong, string>();
            foreach (string raw in File.ReadAllLines(listFile))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string hex = line.Substring(eq + 1).Trim();
                int space = hex.IndexOfAny(new char[] { ' ', '\t', '#' });
                if (space > 0) hex = hex.Substring(0, space);
                wanted[ulong.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture)] = key;
            }

            Directory.CreateDirectory(outDir);
            int found = 0;
            foreach (Entry e in Entries(gameRoot))
            {
                string key;
                if (!wanted.TryGetValue(e.Hash, out key)) continue;
                using (Bitmap bmp = DecodeDds(Read(e)))
                using (Bitmap trimmed = Trim(bmp))
                    trimmed.Save(Path.Combine(outDir, key + ".png"), ImageFormat.Png);
                Console.WriteLine("exported " + key + " from " + e.Pack);
                wanted.Remove(e.Hash);
                found++;
            }
            foreach (KeyValuePair<ulong, string> kv in wanted)
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "NOT FOUND {0} = {1:X16}", kv.Value, kv.Key));
            return wanted.Count == 0 ? 0 : 3;
        }

        // ---------- pack2 ----------

        static IEnumerable<Entry> Entries(string gameRoot)
        {
            foreach (string pack in UiPacks)
            {
                string path = Path.Combine(Path.Combine(gameRoot, @"Resources\Assets"), pack);
                using (FileStream fs = OpenRead(path))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    byte[] magic = br.ReadBytes(4);
                    if (magic[0] != 'P' || magic[1] != 'A' || magic[2] != 'K') throw new InvalidDataException("not a pack2: " + path);
                    uint count = br.ReadUInt32();
                    br.ReadUInt64();
                    ulong mapOffset = br.ReadUInt64();
                    fs.Position = (long)mapOffset;
                    for (uint i = 0; i < count; i++)
                    {
                        Entry e = new Entry();
                        e.Pack = pack;
                        e.PackPath = path;
                        e.Hash = br.ReadUInt64();
                        e.Offset = br.ReadUInt64();
                        e.Length = br.ReadUInt64();
                        br.ReadUInt32();
                        br.ReadUInt32();
                        yield return e;
                    }
                }
            }
        }

        static FileStream OpenRead(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }

        static string currentPack;
        static FileStream currentStream;

        static byte[] Read(Entry e)
        {
            if (currentPack != e.PackPath)
            {
                if (currentStream != null) currentStream.Dispose();
                currentStream = OpenRead(e.PackPath);
                currentPack = e.PackPath;
            }
            currentStream.Position = (long)e.Offset;
            byte[] raw = new byte[e.Length];
            int total = 0;
            while (total < raw.Length)
            {
                int r = currentStream.Read(raw, total, raw.Length - total);
                if (r <= 0) break;
                total += r;
            }

            if (raw.Length > 10 && raw[0] == 0xA1 && raw[1] == 0xB2 && raw[2] == 0xC3 && raw[3] == 0xD4)
            {
                int size = (raw[4] << 24) | (raw[5] << 16) | (raw[6] << 8) | raw[7];
                byte[] outBuf = new byte[size];
                using (MemoryStream ms = new MemoryStream(raw, 10, raw.Length - 10))
                using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
                {
                    int t = 0;
                    while (t < size)
                    {
                        int r = ds.Read(outBuf, t, size - t);
                        if (r <= 0) break;
                        t += r;
                    }
                }
                return outBuf;
            }
            return raw;
        }

        // ---------- DDS ----------

        static bool IsDds(byte[] d)
        {
            return d.Length > 128 && d[0] == 'D' && d[1] == 'D' && d[2] == 'S' && d[3] == ' ';
        }

        static Bitmap DecodeDds(byte[] d)
        {
            if (!IsDds(d)) return null;
            int height = BitConverter.ToInt32(d, 12);
            int width = BitConverter.ToInt32(d, 16);
            int pfFlags = BitConverter.ToInt32(d, 80);
            string fourCC = Encoding.ASCII.GetString(d, 84, 4);
            int dataStart = 128;
            byte[] bgra = new byte[width * height * 4];

            if ((pfFlags & 0x4) != 0 && fourCC == "DXT5") DecodeDxt5(d, dataStart, width, height, bgra);
            else if ((pfFlags & 0x4) != 0 && fourCC == "DXT1") DecodeDxt1(d, dataStart, width, height, bgra);
            else if ((pfFlags & 0x40) != 0 && BitConverter.ToInt32(d, 88) == 32) DecodeRgb32(d, dataStart, width, height, bgra);
            else return null;

            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < height; y++)
                Marshal.Copy(bgra, y * width * 4, IntPtr.Add(bd.Scan0, y * bd.Stride), width * 4);
            bmp.UnlockBits(bd);
            return bmp;
        }

        static void DecodeRgb32(byte[] d, int start, int w, int h, byte[] bgra)
        {
            uint rMask = BitConverter.ToUInt32(d, 92), gMask = BitConverter.ToUInt32(d, 96);
            uint bMask = BitConverter.ToUInt32(d, 100), aMask = BitConverter.ToUInt32(d, 104);
            for (int i = 0; i < w * h; i++)
            {
                uint px = BitConverter.ToUInt32(d, start + i * 4);
                bgra[i * 4 + 0] = Channel(px, bMask);
                bgra[i * 4 + 1] = Channel(px, gMask);
                bgra[i * 4 + 2] = Channel(px, rMask);
                bgra[i * 4 + 3] = aMask == 0 ? (byte)255 : Channel(px, aMask);
            }
        }

        static byte Channel(uint px, uint mask)
        {
            if (mask == 0) return 0;
            int shift = 0;
            while (((mask >> shift) & 1) == 0) shift++;
            return (byte)((px & mask) >> shift);
        }

        static void DecodeDxt1(byte[] d, int start, int w, int h, byte[] bgra)
        {
            int bw = (w + 3) / 4, bh = (h + 3) / 4;
            for (int by = 0; by < bh; by++)
                for (int bx = 0; bx < bw; bx++)
                {
                    int off = start + (by * bw + bx) * 8;
                    byte[] alpha = { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 };
                    ColorBlock(d, off, bx, by, w, h, bgra, alpha, true);
                }
        }

        static void DecodeDxt5(byte[] d, int start, int w, int h, byte[] bgra)
        {
            int bw = (w + 3) / 4, bh = (h + 3) / 4;
            byte[] alpha = new byte[16];
            byte[] table = new byte[8];
            for (int by = 0; by < bh; by++)
                for (int bx = 0; bx < bw; bx++)
                {
                    int off = start + (by * bw + bx) * 16;
                    byte a0 = d[off], a1 = d[off + 1];
                    table[0] = a0;
                    table[1] = a1;
                    if (a0 > a1)
                        for (int i = 1; i <= 6; i++) table[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
                    else
                    {
                        for (int i = 1; i <= 4; i++) table[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
                        table[6] = 0;
                        table[7] = 255;
                    }
                    ulong bits = 0;
                    for (int i = 0; i < 6; i++) bits |= (ulong)d[off + 2 + i] << (8 * i);
                    for (int i = 0; i < 16; i++) alpha[i] = table[(bits >> (3 * i)) & 7];
                    ColorBlock(d, off + 8, bx, by, w, h, bgra, alpha, false);
                }
        }

        static void ColorBlock(byte[] d, int off, int bx, int by, int w, int h, byte[] bgra, byte[] alpha, bool dxt1)
        {
            int c0 = d[off] | (d[off + 1] << 8);
            int c1 = d[off + 2] | (d[off + 3] << 8);
            int[] r = new int[4], g = new int[4], b = new int[4];
            Unpack565(c0, out r[0], out g[0], out b[0]);
            Unpack565(c1, out r[1], out g[1], out b[1]);
            bool fourColor = !dxt1 || c0 > c1;
            if (fourColor)
            {
                r[2] = (2 * r[0] + r[1]) / 3; g[2] = (2 * g[0] + g[1]) / 3; b[2] = (2 * b[0] + b[1]) / 3;
                r[3] = (r[0] + 2 * r[1]) / 3; g[3] = (g[0] + 2 * g[1]) / 3; b[3] = (b[0] + 2 * b[1]) / 3;
            }
            else
            {
                r[2] = (r[0] + r[1]) / 2; g[2] = (g[0] + g[1]) / 2; b[2] = (b[0] + b[1]) / 2;
                r[3] = 0; g[3] = 0; b[3] = 0;
            }
            uint idx = BitConverter.ToUInt32(d, off + 4);
            for (int i = 0; i < 16; i++)
            {
                int x = bx * 4 + (i & 3), y = by * 4 + (i >> 2);
                if (x >= w || y >= h) continue;
                int k = (int)((idx >> (2 * i)) & 3);
                int p = (y * w + x) * 4;
                bgra[p] = (byte)b[k];
                bgra[p + 1] = (byte)g[k];
                bgra[p + 2] = (byte)r[k];
                bgra[p + 3] = dxt1 && !fourColor && k == 3 ? (byte)0 : alpha[i];
            }
        }

        static void Unpack565(int c, out int r, out int g, out int b)
        {
            r = ((c >> 11) & 31) * 255 / 31;
            g = ((c >> 5) & 63) * 255 / 63;
            b = (c & 31) * 255 / 31;
        }

        // ---------- images ----------

        static Bitmap Trim(Bitmap src)
        {
            int minX = src.Width, minY = src.Height, maxX = -1, maxY = -1;
            BitmapData bd = src.LockBits(new Rectangle(0, 0, src.Width, src.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] px = new byte[bd.Stride * src.Height];
            Marshal.Copy(bd.Scan0, px, 0, px.Length);
            src.UnlockBits(bd);
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                    if (px[y * bd.Stride + x * 4 + 3] > 8)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
            if (maxX < 0) return new Bitmap(src);
            Rectangle r = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            return src.Clone(r, PixelFormat.Format32bppArgb);
        }

        static void WriteSheet(List<Bitmap> items, int firstIndex, string path)
        {
            const int cell = 128, cols = 10, label = 14;
            int rows = (items.Count + cols - 1) / cols;
            using (Bitmap sheet = new Bitmap(cols * cell, rows * (cell + label), PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(sheet))
            using (Font f = new Font("Consolas", 9f, GraphicsUnit.Pixel))
            using (Brush dark = new SolidBrush(Color.FromArgb(40, 40, 46)))
            using (Brush darker = new SolidBrush(Color.FromArgb(28, 28, 32)))
            {
                g.Clear(Color.FromArgb(20, 20, 24));
                for (int i = 0; i < items.Count; i++)
                {
                    int cx = (i % cols) * cell, cy = (i / cols) * (cell + label);
                    for (int yy = 0; yy < cell; yy += 16)
                        for (int xx = 0; xx < cell; xx += 16)
                            g.FillRectangle(((xx + yy) / 16) % 2 == 0 ? dark : darker, cx + xx, cy + yy, 16, 16);
                    Bitmap b = items[i];
                    float s = Math.Min((cell - 4f) / b.Width, (cell - 4f) / b.Height);
                    int dw = Math.Max(1, (int)(b.Width * s)), dh = Math.Max(1, (int)(b.Height * s));
                    g.DrawImage(b, cx + (cell - dw) / 2, cy + (cell - dh) / 2, dw, dh);
                    g.DrawString((firstIndex + i).ToString(CultureInfo.InvariantCulture) + " " + b.Width + "x" + b.Height,
                        f, Brushes.Gainsboro, cx + 2, cy + cell);
                }
                sheet.Save(path, ImageFormat.Png);
            }
        }
    }
}
