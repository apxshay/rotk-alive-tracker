using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace RotkAlive.App
{
    // Opened from the tray menu or the edit-mode toolbar. Changes apply to the panel at once;
    // the owner saves overlay.ini when this window closes.
    sealed class SettingsForm : Form
    {
        readonly Settings settings;
        readonly Action<bool> changed;
        readonly TrackBar opacity = new TrackBar();
        readonly Label opacityValue = new Label();
        readonly CheckBox hints = new CheckBox();
        readonly System.Collections.Generic.List<Font> fonts = new System.Collections.Generic.List<Font>();

        public SettingsForm(Settings settings, string[] shortcuts, Action<bool> changed, Action resetPosition)
        {
            this.settings = settings;
            this.changed = changed;

            Text = "ROTK alive tracker settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            TopMost = true;
            StartPosition = FormStartPosition.CenterScreen;
            Font = Own(new Font("Segoe UI", 9.75f));
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(14);

            TableLayoutPanel grid = new TableLayoutPanel();
            grid.AutoSize = true;
            grid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            grid.ColumnCount = 2;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.Dock = DockStyle.Fill;

            Label opacityHeading = Heading("Opacity");
            grid.Controls.Add(opacityHeading, 0, 0);
            grid.SetColumnSpan(opacityHeading, 2);

            opacity.Minimum = (int)Math.Round(Settings.MinOpacity * 100);
            opacity.Maximum = 100;
            opacity.TickFrequency = 5;
            opacity.SmallChange = 5;
            opacity.LargeChange = 10;
            opacity.Width = 300;
            opacity.Value = Math.Max(opacity.Minimum, Math.Min(100, (int)Math.Round(settings.Opacity * 100)));
            opacity.ValueChanged += delegate { OnOpacity(); };
            grid.Controls.Add(opacity, 0, 1);

            opacityValue.AutoSize = true;
            opacityValue.Anchor = AnchorStyles.Left;
            opacityValue.Font = Own(new Font(Font, FontStyle.Bold));
            grid.Controls.Add(opacityValue, 1, 1);
            ShowOpacity();

            Label opacityNote = Note("Lower is more see-through. Edit mode is always drawn solid.");
            grid.Controls.Add(opacityNote, 0, 2);
            grid.SetColumnSpan(opacityNote, 2);

            hints.Text = "Show the shortcut hint on the panel for 20 seconds after start";
            hints.AutoSize = true;
            hints.Checked = settings.ShowHints;
            hints.Margin = new Padding(3, 12, 3, 3);
            hints.CheckedChanged += delegate
            {
                settings.ShowHints = hints.Checked;
                changed(hints.Checked);
            };
            grid.Controls.Add(hints, 0, 3);
            grid.SetColumnSpan(hints, 2);

            Label keysHeading = Heading("Shortcuts");
            grid.Controls.Add(keysHeading, 0, 4);
            grid.SetColumnSpan(keysHeading, 2);
            Label keys = Note(string.Join(Environment.NewLine, shortcuts));
            keys.ForeColor = SystemColors.ControlText;
            grid.Controls.Add(keys, 0, 5);
            grid.SetColumnSpan(keys, 2);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.AutoSize = true;
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.Margin = new Padding(0, 16, 0, 0);
            Button reset = new Button();
            reset.Text = "Reset position";
            reset.AutoSize = true;
            reset.Click += delegate { resetPosition(); };
            Button close = new Button();
            close.Text = "Close";
            close.AutoSize = true;
            close.Click += delegate { Close(); };
            actions.Controls.Add(reset);
            actions.Controls.Add(close);
            grid.Controls.Add(actions, 0, 6);
            grid.SetColumnSpan(actions, 2);

            Controls.Add(grid);
            AcceptButton = close;
            CancelButton = close;
        }

        Font Own(Font f)
        {
            fonts.Add(f);
            return f;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                foreach (Font f in fonts) f.Dispose();
                fonts.Clear();
            }
        }

        void OnOpacity()
        {
            settings.Opacity = opacity.Value / 100.0;
            ShowOpacity();
            changed(false);
        }

        void ShowOpacity()
        {
            opacityValue.Text = opacity.Value.ToString(CultureInfo.InvariantCulture) + " %";
        }

        Label Heading(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Font = Own(new Font(Font, FontStyle.Bold));
            l.Margin = new Padding(3, 10, 3, 3);
            return l;
        }

        static Label Note(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = SystemColors.GrayText;
            return l;
        }
    }
}
