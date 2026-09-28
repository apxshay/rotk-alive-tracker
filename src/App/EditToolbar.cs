using System;
using System.Collections.Generic;
using System.Drawing;

namespace RotkAlive.App
{
    public enum ToolbarButton { Settings, Hide, Done }

    public sealed class ButtonRect
    {
        public ToolbarButton Id;
        public string Label;
        public Rectangle Bounds;
    }

    // The row of buttons shown at the top of the panel in edit mode. Rectangles are in panel
    // pixels, which are also the window's client pixels.
    public static class EditToolbar
    {
        static readonly ToolbarButton[] Order = { ToolbarButton.Settings, ToolbarButton.Hide, ToolbarButton.Done };
        static readonly string[] Labels = { "SETTINGS", "HIDE", "DONE" };

        public static List<ButtonRect> Layout(int left, int top, int width, int height, int gap)
        {
            List<ButtonRect> list = new List<ButtonRect>();
            int n = Order.Length;
            gap = Math.Max(0, gap);
            int each = Math.Max(1, (width - gap * (n - 1)) / n);
            int x = left;
            for (int i = 0; i < n; i++)
            {
                int w = i == n - 1 ? Math.Max(1, left + width - x) : each;
                ButtonRect b = new ButtonRect();
                b.Id = Order[i];
                b.Label = Labels[i];
                b.Bounds = new Rectangle(x, top, w, height);
                list.Add(b);
                x += w + gap;
            }
            return list;
        }

        public static ButtonRect HitTest(List<ButtonRect> buttons, Point p)
        {
            if (buttons == null) return null;
            foreach (ButtonRect b in buttons)
                if (b.Bounds.Contains(p)) return b;
            return null;
        }
    }
}
