using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class Theme
{
    internal static readonly Color Background = Color.FromArgb(22, 16, 18);
    internal static readonly Color Surface = Color.FromArgb(48, 20, 27);
    internal static readonly Color Foreground = Color.FromArgb(244, 234, 236);
    internal static readonly Color Accent = Color.FromArgb(205, 45, 65);

    internal static NumericUpDown Number(decimal min, decimal max, decimal value, decimal step) { return new NumericUpDown { Minimum = min, Maximum = max, Value = value, Increment = step, Width = 125, ThousandsSeparator = true }; }
    internal static TrackBar Slider(int min, int max, int value, int tick) { return new TrackBar { Minimum = min, Maximum = max, Value = value, TickFrequency = tick, TickStyle = TickStyle.None, Dock = DockStyle.Fill, Height = 34 }; }
    internal static Button AccentButton(string text, Color color) { return new Button { Text = text, AutoSize = true, Height = 34, Padding = new Padding(10, 3, 10, 3), FlatStyle = FlatStyle.Flat, BackColor = color, ForeColor = Color.White, Cursor = Cursors.Hand }; }
    internal static Label SectionLabel(string text) { return new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI Semibold", 10), ForeColor = Color.FromArgb(235, 75, 95), Padding = new Padding(0, 8, 0, 2) }; }

    internal static void Apply(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is TextBox || control is ComboBox || control is NumericUpDown) { control.BackColor = Color.FromArgb(55, 31, 37); control.ForeColor = Color.White; }
            else if (!(control is Button) && !(control is TrackBar) && !(control is Label)) { if (control.BackColor == SystemColors.Control) control.BackColor = root.BackColor; control.ForeColor = root.ForeColor; }
            Apply(control);
        }
    }

    internal static Image LoadEmbeddedImage(string name)
    {
        try
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) return null;
                using (Image source = Image.FromStream(stream)) return new Bitmap(source);
            }
        }
        catch { return null; }
    }
}
