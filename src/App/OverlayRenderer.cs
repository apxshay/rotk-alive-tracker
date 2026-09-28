using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace RotkAlive.App
{
    sealed class PanelRow
    {
        public string IconKey;
        public string Name;
        public int Kills;
        public Color Color;
    }

    sealed class PanelModel
    {
        public string Title;
        public string Status;
        public string SubLine;
        public bool SubLineGold;
        public readonly List<PanelRow> Rows = new List<PanelRow>();
        public string Footer;
        public string Hint;
        public bool Stale;
        public bool MoveMode;
        // Filled by OverlayRenderer.Render in move mode.
        public List<ButtonRect> Buttons = new List<ButtonRect>();
    }

    // Draws the panel into a 32 bpp image with per-pixel alpha. Every size is a multiple of the
    // screen height so the look does not depend on the game's resolution.
    static class OverlayRenderer
    {
        static readonly Color PanelBack = Color.FromArgb(18, 18, 18);
        static readonly Color CardBack = Color.FromArgb(26, 26, 27);
        static readonly Color CardEdge = Color.FromArgb(46, 45, 43);
        static readonly Color FrameOuter = Color.FromArgb(92, 82, 70);
        static readonly Color FrameInner = Color.FromArgb(38, 34, 30);
        static readonly Color Rivet = Color.FromArgb(128, 114, 96);
        static readonly Color TitleColor = Color.FromArgb(217, 212, 204);
        static readonly Color GoldText = Color.FromArgb(214, 178, 94);
        static readonly Color GoldRule = Color.FromArgb(140, 116, 64);
        static readonly Color DimText = Color.FromArgb(150, 148, 144);
        static readonly Color StaleText = Color.FromArgb(118, 118, 118);
        static readonly Color MoveBorder = Color.FromArgb(255, 204, 51);
        static readonly Color ButtonBack = Color.FromArgb(44, 40, 34);
        static readonly Color ButtonText = Color.FromArgb(236, 226, 206);
        static readonly Color DoneText = Color.FromArgb(24, 20, 14);

        public static readonly Color PlacementColor = Color.FromArgb(160, 160, 160);
        static readonly Dictionary<string, Color> TierColors = new Dictionary<string, Color>
        {
            { "placement", PlacementColor },
            { "bronze", Color.FromArgb(226, 138, 72) },
            { "silver", Color.FromArgb(206, 211, 219) },
            { "gold", Color.FromArgb(240, 196, 60) },
            { "platinum", Color.FromArgb(120, 214, 236) },
            { "diamond", Color.FromArgb(160, 124, 255) },
            { "master", Color.FromArgb(84, 222, 140) },
            { "royalty", Color.FromArgb(255, 170, 64) },
            { "royalty-one", Color.FromArgb(240, 76, 140) },
        };

        public static Color TierColor(string iconKey)
        {
            Color c;
            return TierColors.TryGetValue(iconKey, out c) ? c : PlacementColor;
        }

        sealed class Metrics
        {
            public float U;
            public int Width, Pad, Frame, TitleH, SubH, RowH, Gap, FooterH, IconH, ToolH, ToolHintH, HintH;
            public Font Title, Status, Sub, Name, NameFallback, Kills, Footer, Button, Hint;
        }

        public static Bitmap Render(PanelModel m, int screenHeight, Settings s)
        {
            Metrics k = Measure(screenHeight, s);
            try
            {
                int height = k.Frame * 2 + k.Pad + k.TitleH + (m.SubLine != null ? k.SubH : 0) + k.Pad / 2;
                if (m.MoveMode) height += k.ToolH + k.ToolHintH + k.Pad / 2;
                if (m.Rows.Count > 0) height += m.Rows.Count * (k.RowH + k.Gap);
                if (m.Footer != null) height += k.FooterH;
                if (m.Hint != null && !m.MoveMode) height += k.HintH;
                height += k.Pad;
                m.Buttons.Clear();

                Bitmap bmp = new Bitmap(k.Width, height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                    int panelAlpha = (int)Math.Round(255 * s.PanelOpacity);
                    DrawFrame(g, k, bmp.Width, bmp.Height, panelAlpha, m.MoveMode);

                    float x = k.Frame + k.Pad;
                    float inner = bmp.Width - 2 * (k.Frame + k.Pad);
                    float y = k.Frame + k.Pad * 0.6f;

                    if (m.MoveMode)
                    {
                        m.Buttons.AddRange(EditToolbar.Layout((int)x, (int)y, (int)inner, k.ToolH, k.Gap * 2));
                        foreach (ButtonRect b in m.Buttons) DrawButton(g, k, b);
                        y += k.ToolH;
                        DrawText(g, "DRAG THE PANEL TO MOVE IT", k.Hint, GoldText,
                            new RectangleF(x, y, inner, k.ToolHintH), StringAlignment.Center);
                        y += k.ToolHintH + k.Pad / 2;
                    }

                    Color titleColor = m.Stale ? StaleText : TitleColor;
                    DrawText(g, m.Title, k.Title, titleColor, new RectangleF(x, y, inner, k.TitleH), StringAlignment.Near);
                    if (m.Status != null)
                        DrawText(g, m.Status, k.Status, m.Stale ? StaleText : DimText,
                            new RectangleF(x, y, inner, k.TitleH), StringAlignment.Far);
                    y += k.TitleH;

                    using (Pen rule = new Pen(Color.FromArgb(m.Stale ? 90 : 200, GoldRule), Math.Max(1f, k.U * 0.07f)))
                        g.DrawLine(rule, x, y, x + inner, y);

                    if (m.SubLine != null)
                    {
                        Color subColor = m.Stale ? StaleText : (m.SubLineGold ? GoldText : DimText);
                        DrawText(g, m.SubLine, k.Sub, subColor, new RectangleF(x, y + k.SubH * 0.12f, inner, k.SubH), StringAlignment.Near);
                        y += k.SubH;
                    }
                    y += k.Pad / 2;

                    foreach (PanelRow r in m.Rows)
                    {
                        DrawRow(g, k, r, new RectangleF(x - k.Pad * 0.35f, y, inner + k.Pad * 0.7f, k.RowH), m.Stale, panelAlpha);
                        y += k.RowH + k.Gap;
                    }

                    if (m.Footer != null)
                    {
                        DrawText(g, m.Footer, k.Footer, m.Stale ? StaleText : DimText,
                            new RectangleF(x, y, inner, k.FooterH), StringAlignment.Center);
                        y += k.FooterH;
                    }

                    if (m.Hint != null && !m.MoveMode)
                        DrawText(g, m.Hint, k.Hint, GoldText, new RectangleF(x, y, inner, k.HintH), StringAlignment.Center);
                }
                return bmp;
            }
            finally
            {
                foreach (Font f in new Font[] { k.Title, k.Status, k.Sub, k.Name, k.NameFallback, k.Kills, k.Footer, k.Button, k.Hint })
                    if (f != null) f.Dispose();
            }
        }

        static Metrics Measure(int screenHeight, Settings s)
        {
            Metrics k = new Metrics();
            float u = (float)Math.Max(9.0, screenHeight * s.FontScale);
            k.U = u;
            k.Width = (int)Math.Round(Math.Max(u * 14, screenHeight * s.PanelWidthScale));
            k.Frame = Math.Max(2, (int)Math.Round(u * 0.22));
            k.Pad = (int)Math.Round(u * 0.85);
            k.TitleH = (int)Math.Round(u * 2.1);
            k.SubH = (int)Math.Round(u * 1.45);
            k.RowH = (int)Math.Round(u * 2.55);
            k.Gap = Math.Max(2, (int)Math.Round(u * 0.22));
            k.FooterH = (int)Math.Round(u * 1.6);
            k.IconH = (int)Math.Round(k.RowH * 0.88);
            k.ToolH = (int)Math.Round(u * 2.2);
            k.ToolHintH = (int)Math.Round(u * 1.5);
            k.HintH = (int)Math.Round(u * 1.4);

            FontFamily oswald = Assets.Oswald;
            k.Title = new Font(oswald, u * 1.6f, FontStyle.Bold, GraphicsUnit.Pixel);
            k.Status = new Font(oswald, u * 0.8f, FontStyle.Bold, GraphicsUnit.Pixel);
            k.Sub = new Font(oswald, u * 0.95f, FontStyle.Bold, GraphicsUnit.Pixel);
            k.Name = new Font(oswald, u * 1.3f, FontStyle.Bold, GraphicsUnit.Pixel);
            k.NameFallback = new Font(Assets.FallbackFamily, u * 1.1f, FontStyle.Bold, GraphicsUnit.Pixel);
            k.Kills = new Font(oswald, u * 1.35f, FontStyle.Bold, GraphicsUnit.Pixel);
            k.Footer = new Font(oswald, u * 0.9f, FontStyle.Bold, GraphicsUnit.Pixel);
            k.Button = new Font(oswald, u * 0.95f, FontStyle.Bold, GraphicsUnit.Pixel);
            k.Hint = new Font(oswald, u * 0.8f, FontStyle.Bold, GraphicsUnit.Pixel);
            return k;
        }

        static void DrawButton(Graphics g, Metrics k, ButtonRect b)
        {
            bool done = b.Id == ToolbarButton.Done;
            Rectangle r = b.Bounds;
            using (Brush back = new SolidBrush(done ? MoveBorder : ButtonBack))
                g.FillRectangle(back, r);
            using (Pen edge = new Pen(done ? MoveBorder : GoldRule, Math.Max(1f, k.U * 0.08f)))
                g.DrawRectangle(edge, r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1);
            DrawText(g, b.Label, k.Button, done ? DoneText : ButtonText, r, StringAlignment.Center);
        }

        static void DrawFrame(Graphics g, Metrics k, int w, int h, int alpha, bool moveMode)
        {
            Rectangle outer = new Rectangle(0, 0, w - 1, h - 1);
            using (Brush back = new SolidBrush(Color.FromArgb(alpha, PanelBack)))
                g.FillRectangle(back, outer);

            float fw = k.Frame;
            using (Pen o = new Pen(FrameOuter, fw))
                g.DrawRectangle(o, fw / 2, fw / 2, w - fw, h - fw);
            using (Pen i = new Pen(FrameInner, Math.Max(1f, fw / 2)))
                g.DrawRectangle(i, fw + 0.5f, fw + 0.5f, w - 2 * fw - 1, h - 2 * fw - 1);
            using (Pen hi = new Pen(Color.FromArgb(70, 255, 240, 220), 1f))
                g.DrawLine(hi, fw, 0.5f, w - fw, 0.5f);

            float rv = Math.Max(3f, k.U * 0.32f);
            using (Brush rivet = new SolidBrush(Rivet))
            using (Brush rivetDark = new SolidBrush(Color.FromArgb(40, 34, 28)))
            {
                foreach (PointF p in new PointF[] { new PointF(0, 0), new PointF(w - rv - 1, 0), new PointF(0, h - rv - 1), new PointF(w - rv - 1, h - rv - 1) })
                {
                    g.FillRectangle(rivetDark, p.X, p.Y, rv + 1, rv + 1);
                    g.FillRectangle(rivet, p.X + 1, p.Y + 1, rv - 1, rv - 1);
                }
            }

            if (moveMode)
            {
                using (Pen mv = new Pen(MoveBorder, Math.Max(2f, fw)) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(mv, fw, fw, w - 2 * fw - 1, h - 2 * fw - 1);
            }
        }

        static void DrawRow(Graphics g, Metrics k, PanelRow r, RectangleF card, bool stale, int panelAlpha)
        {
            int cardAlpha = Math.Min(255, panelAlpha + 20);
            using (Brush b = new SolidBrush(Color.FromArgb(cardAlpha, CardBack)))
                g.FillRectangle(b, card);
            using (Pen p = new Pen(CardEdge, 1f))
                g.DrawRectangle(p, card.X + 0.5f, card.Y + 0.5f, card.Width - 1, card.Height - 1);

            float iconX = card.X + k.U * 0.45f;
            Bitmap icon = Assets.Icon(r.IconKey);
            float iconW = k.IconH;
            if (icon != null)
            {
                iconW = k.IconH * icon.Width / (float)icon.Height;
                RectangleF dst = new RectangleF(iconX, card.Y + (card.Height - k.IconH) / 2f, iconW, k.IconH);
                if (stale) DrawGrey(g, icon, dst);
                else g.DrawImage(icon, dst);
            }

            Color c = stale ? StaleText : r.Color;
            float killsW = k.U * 2.4f;
            float nameX = iconX + iconW + k.U * 0.7f;
            RectangleF killsRect = new RectangleF(card.Right - k.U * 0.7f - killsW, card.Y, killsW, card.Height);
            RectangleF nameRect = new RectangleF(nameX, card.Y, killsRect.X - nameX - k.U * 0.3f, card.Height);

            Font nameFont = Assets.OswaldCovers(r.Name) ? k.Name : k.NameFallback;
            DrawText(g, r.Name, nameFont, c, nameRect, StringAlignment.Near);
            DrawText(g, r.Kills.ToString(System.Globalization.CultureInfo.InvariantCulture), k.Kills, c, killsRect, StringAlignment.Far);
        }

        static void DrawGrey(Graphics g, Bitmap icon, RectangleF dst)
        {
            ColorMatrix cm = new ColorMatrix(new float[][]
            {
                new float[] { 0.3f, 0.3f, 0.3f, 0, 0 },
                new float[] { 0.59f, 0.59f, 0.59f, 0, 0 },
                new float[] { 0.11f, 0.11f, 0.11f, 0, 0 },
                new float[] { 0, 0, 0, 0.6f, 0 },
                new float[] { 0, 0, 0, 0, 1 },
            });
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetColorMatrix(cm);
                g.DrawImage(icon, Rectangle.Round(dst), 0, 0, icon.Width, icon.Height, GraphicsUnit.Pixel, ia);
            }
        }

        static void DrawText(Graphics g, string text, Font font, Color color, RectangleF rect, StringAlignment align)
        {
            if (string.IsNullOrEmpty(text)) return;
            using (StringFormat sf = new StringFormat(StringFormatFlags.NoWrap))
            using (Brush b = new SolidBrush(color))
            {
                sf.Alignment = align;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(text, font, b, rect, sf);
            }
        }
    }
}
