using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Macro Do Butler")]
[assembly: AssemblyProduct("Macro Do Butler")]
[assembly: AssemblyDescription("Utilitário configurável de controle de movimento")]

internal static class Program
{
    static Mutex instanceMutex;
    [STAThread]
    private static void Main()
    {
        bool first;
        instanceMutex = new Mutex(true, "MacroDoButler_UnicaInstancia_6B5E", out first);
        if (!first) { MessageBox.Show("O programa já está aberto perto do relógio.", "Macro Do Butler", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    const int VK_LBUTTON = 1, VK_RBUTTON = 2, VK_F1 = 0x70, VK_F2 = 0x71, VK_F12 = 0x7B;
    const uint MOVE = 1;
    NumericUpDown vertical, horizontal, interval;
    TrackBar verticalSlider, horizontalSlider, intervalSlider;
    NumericUpDown secondaryVertical, secondaryHorizontal;
    TrackBar secondaryVerticalSlider, secondaryHorizontalSlider;
    CheckBox requireRight;
    ComboBox presets;
    TextBox presetName;
    Button savePreset, deletePreset;
    Label status;
    Label weaponStatus;
    Button toggle;
    Button toggleWeapon;
    NotifyIcon tray;
    volatile bool active, closing, secondaryActive;

    public MainForm()
    {
        Text = "Macro Do Butler";
        ClientSize = new Size(650, 810);
        MinimumSize = new Size(666, 849);
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        BackColor = Color.FromArgb(22, 16, 18);
        ForeColor = Color.FromArgb(244, 234, 236);
        vertical = Number(0, 10000, 350, 25);
        horizontal = Number(-5000, 5000, 0, 25);
        interval = Number(2, 50, 7, 1);
        verticalSlider = Slider(0, 10000, 350, 500);
        horizontalSlider = Slider(-5000, 5000, 0, 500);
        intervalSlider = Slider(2, 50, 7, 4);
        secondaryVertical = Number(0, 10000, 0, 25);
        secondaryHorizontal = Number(-5000, 5000, 0, 25);
        secondaryVerticalSlider = Slider(0, 10000, 0, 500);
        secondaryHorizontalSlider = Slider(-5000, 5000, 0, 500);
        requireRight = new CheckBox { Text = "Exigir botão direito pressionado", Checked = true, AutoSize = true };

        presets = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        presetName = new TextBox { Width = 230 };
        savePreset = AccentButton("SALVAR PRESET", Color.FromArgb(205, 45, 65));
        deletePreset = AccentButton("EXCLUIR", Color.FromArgb(120, 35, 48));
        var presetButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        presetButtons.Controls.Add(savePreset); presetButtons.Controls.Add(deletePreset);

        var header = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Color.FromArgb(48, 20, 27), Padding = new Padding(18, 10, 22, 8) };
        var brandImage = LoadEmbeddedImage("MacroDoButler.Brand");
        if (brandImage != null) header.Controls.Add(new PictureBox { Image = brandImage, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(72, 72), Location = new Point(14, 11), BackColor = Color.Transparent });
        header.Controls.Add(new Label { Text = "MACRO DO BUTLER", Font = new Font("Segoe UI Semibold", 17), ForeColor = Color.White, AutoSize = true, Location = new Point(98, 18) });
        header.Controls.Add(new Label { Text = "Presets persistentes • ajuste em tempo real", ForeColor = Color.FromArgb(205, 150, 160), AutoSize = true, Location = new Point(101, 54) });

        var grid = new TableLayoutPanel { ColumnCount = 2, RowCount = 18, Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 10), BackColor = Color.FromArgb(22, 16, 18) };
        grid.BackgroundImage = LoadEmbeddedImage("MacroDoButler.Watermark");
        grid.BackgroundImageLayout = ImageLayout.Zoom;
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        Row(grid, "Carregar preset", presets, 0);
        Row(grid, "Nome para salvar/atualizar", presetName, 1);
        grid.Controls.Add(presetButtons, 1, 2);
        grid.Controls.Add(SectionLabel("ARMA PRIMÁRIA"), 0, 3); grid.SetColumnSpan(grid.GetControlFromPosition(0, 3), 2);
        Row(grid, "Força vertical (0–10.000 px/s)", vertical, 4);
        grid.Controls.Add(verticalSlider, 0, 5); grid.SetColumnSpan(verticalSlider, 2);
        Row(grid, "Correção lateral (-5.000 a 5.000 px/s)", horizontal, 6);
        grid.Controls.Add(horizontalSlider, 0, 7); grid.SetColumnSpan(horizontalSlider, 2);
        grid.Controls.Add(SectionLabel("ARMA SECUNDÁRIA"), 0, 8); grid.SetColumnSpan(grid.GetControlFromPosition(0, 8), 2);
        Row(grid, "Força vertical secundária", secondaryVertical, 9);
        grid.Controls.Add(secondaryVerticalSlider, 0, 10); grid.SetColumnSpan(secondaryVerticalSlider, 2);
        Row(grid, "Correção lateral secundária", secondaryHorizontal, 11);
        grid.Controls.Add(secondaryHorizontalSlider, 0, 12); grid.SetColumnSpan(secondaryHorizontalSlider, 2);
        Row(grid, "Intervalo compartilhado (2–50 ms)", interval, 13);
        grid.Controls.Add(intervalSlider, 0, 14); grid.SetColumnSpan(intervalSlider, 2);
        grid.Controls.Add(requireRight, 0, 15); grid.SetColumnSpan(requireRight, 2);
        var help = new Label { Text = "F1 liga/desliga  •  F2 alterna primária/secundária  •  F12 encerra", AutoSize = true, MaximumSize = new Size(580, 0), ForeColor = Color.FromArgb(190, 140, 150) };
        grid.Controls.Add(help, 0, 16); grid.SetColumnSpan(help, 2);

        status = new Label { Height = 42, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 11) };
        toggle = AccentButton("LIGAR (F1)", Color.FromArgb(205, 45, 65)); toggle.Height = 46; toggle.Dock = DockStyle.Top;
        weaponStatus = new Label { Height = 30, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 10), ForeColor = Color.FromArgb(255, 120, 135) };
        toggleWeapon = AccentButton("TROCAR ARMA (F2)", Color.FromArgb(155, 42, 62)); toggleWeapon.Height = 38; toggleWeapon.Dock = DockStyle.Top;
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 176, Padding = new Padding(24, 8, 24, 14), BackColor = Color.FromArgb(48, 20, 27) };
        bottom.Controls.Add(toggle); bottom.Controls.Add(status); bottom.Controls.Add(toggleWeapon); bottom.Controls.Add(weaponStatus);
        Controls.Add(grid); Controls.Add(header); Controls.Add(bottom);

        vertical.ValueChanged += delegate { if (verticalSlider.Value != (int)vertical.Value) verticalSlider.Value = (int)vertical.Value; };
        verticalSlider.ValueChanged += delegate { if (vertical.Value != verticalSlider.Value) vertical.Value = verticalSlider.Value; };
        horizontal.ValueChanged += delegate { if (horizontalSlider.Value != (int)horizontal.Value) horizontalSlider.Value = (int)horizontal.Value; };
        horizontalSlider.ValueChanged += delegate { if (horizontal.Value != horizontalSlider.Value) horizontal.Value = horizontalSlider.Value; };
        interval.ValueChanged += delegate { if (intervalSlider.Value != (int)interval.Value) intervalSlider.Value = (int)interval.Value; };
        intervalSlider.ValueChanged += delegate { if (interval.Value != intervalSlider.Value) interval.Value = intervalSlider.Value; };
        secondaryVertical.ValueChanged += delegate { if (secondaryVerticalSlider.Value != (int)secondaryVertical.Value) secondaryVerticalSlider.Value = (int)secondaryVertical.Value; };
        secondaryVerticalSlider.ValueChanged += delegate { if (secondaryVertical.Value != secondaryVerticalSlider.Value) secondaryVertical.Value = secondaryVerticalSlider.Value; };
        secondaryHorizontal.ValueChanged += delegate { if (secondaryHorizontalSlider.Value != (int)secondaryHorizontal.Value) secondaryHorizontalSlider.Value = (int)secondaryHorizontal.Value; };
        secondaryHorizontalSlider.ValueChanged += delegate { if (secondaryHorizontal.Value != secondaryHorizontalSlider.Value) secondaryHorizontal.Value = secondaryHorizontalSlider.Value; };

        tray = new NotifyIcon { Visible = true, Text = "Macro Do Butler", Icon = Icon };
        toggle.Click += delegate { Toggle(); };
        toggleWeapon.Click += delegate { ToggleWeapon(); };
        savePreset.Click += delegate { SavePreset(); };
        deletePreset.Click += delegate { DeletePreset(); };
        presets.SelectionChangeCommitted += delegate { LoadSelectedPreset(); };
        tray.DoubleClick += delegate { Show(); WindowState = FormWindowState.Normal; Activate(); };
        Resize += delegate { if (WindowState == FormWindowState.Minimized) { Hide(); tray.ShowBalloonTip(700, "Macro Do Butler", "Executando na bandeja. F1 liga/desliga; F2 troca a arma.", ToolTipIcon.Info); } };
        FormClosed += delegate { closing = true; tray.Dispose(); };
        RefreshPresets(null);
        UpdateStatus();
        UpdateWeaponStatus();
        ApplyTheme(this);

        var worker = new Thread(Loop) { IsBackground = true, Name = "Movimento" };
        worker.Start();
    }

    void Loop()
    {
        double rx = 0, ry = 0;
        var watch = Stopwatch.StartNew(); long last = watch.ElapsedTicks;
        bool oldF1 = false, oldF2 = false, oldF12 = false;
        while (!closing)
        {
            int wait = ReadNumber(interval, 7); Thread.Sleep(wait);
            bool f1 = Down(VK_F1), f2 = Down(VK_F2), f12 = Down(VK_F12);
            if (f1 && !oldF1) try { BeginInvoke(new Action(Toggle)); } catch { }
            if (f2 && !oldF2) try { BeginInvoke(new Action(ToggleWeapon)); } catch { }
            if (f12 && !oldF12) try { BeginInvoke(new Action(Close)); } catch { }
            oldF1 = f1; oldF2 = f2; oldF12 = f12;
            long now = watch.ElapsedTicks; double seconds = (now - last) / (double)Stopwatch.Frequency; last = now;
            bool moving = active && Down(VK_LBUTTON) && (!ReadCheck() || Down(VK_RBUTTON));
            if (!moving) { rx = 0; ry = 0; continue; }
            NumericUpDown currentX = secondaryActive ? secondaryHorizontal : horizontal;
            NumericUpDown currentY = secondaryActive ? secondaryVertical : vertical;
            rx += ReadNumber(currentX, 0) * seconds; ry += ReadNumber(currentY, secondaryActive ? 0 : 350) * seconds;
            int x = (int)Math.Truncate(rx), y = (int)Math.Truncate(ry); rx -= x; ry -= y;
            if (x != 0 || y != 0) mouse_event(MOVE, x, y, 0, UIntPtr.Zero);
        }
    }

    int ReadNumber(NumericUpDown n, int fallback)
    {
        try { return n.InvokeRequired ? (int)n.Invoke(new Func<int>(delegate { return (int)n.Value; })) : (int)n.Value; }
        catch { return fallback; }
    }
    bool ReadCheck()
    {
        try { return requireRight.InvokeRequired ? (bool)requireRight.Invoke(new Func<bool>(delegate { return requireRight.Checked; })) : requireRight.Checked; }
        catch { return true; }
    }
    void Toggle() { active = !active; UpdateStatus(); }
    void ToggleWeapon() { secondaryActive = !secondaryActive; UpdateWeaponStatus(); }

    string PresetFile { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "presets.cfg"); } }
    void SavePreset()
    {
        string name = presetName.Text.Trim();
        if (name.Length == 0) { MessageBox.Show("Digite um nome para o preset.", "Preset", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var entries = ReadPresets();
        string line = Uri.EscapeDataString(name) + "|" + (int)vertical.Value + "|" + (int)horizontal.Value + "|" + (int)secondaryVertical.Value + "|" + (int)secondaryHorizontal.Value + "|" + (int)interval.Value + "|" + (requireRight.Checked ? "1" : "0");
        bool replaced = false;
        for (int i = 0; i < entries.Count; i++)
            if (PresetName(entries[i]).Equals(name, StringComparison.OrdinalIgnoreCase)) { entries[i] = line; replaced = true; break; }
        if (!replaced) entries.Add(line);
        try { WritePresets(entries); RefreshPresets(name); MessageBox.Show("Preset '" + name + "' salvo sem alterar os outros.", "Preset", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        catch (Exception ex) { MessageBox.Show("Não foi possível salvar:\r\n" + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    void DeletePreset()
    {
        string name = presets.SelectedItem == null ? presetName.Text.Trim() : presets.SelectedItem.ToString();
        if (name.Length == 0) return;
        var entries = ReadPresets();
        entries.RemoveAll(delegate(string line) { return PresetName(line).Equals(name, StringComparison.OrdinalIgnoreCase); });
        try { WritePresets(entries); RefreshPresets(null); presetName.Text = ""; }
        catch (Exception ex) { MessageBox.Show("Não foi possível excluir:\r\n" + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    void LoadSelectedPreset()
    {
        string wanted = presets.Text;
        foreach (string line in ReadPresets())
        {
            string[] p = line.Split('|'); int v, h, sv = 0, sh = 0, ms;
            bool oldFormat = p.Length == 5;
            bool newFormat = p.Length == 7;
            int msIndex = oldFormat ? 3 : 5, rightIndex = oldFormat ? 4 : 6;
            if ((oldFormat || newFormat) && PresetName(line).Equals(wanted, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(p[1], out v) && int.TryParse(p[2], out h) && int.TryParse(p[msIndex], out ms)
                && (oldFormat || (int.TryParse(p[3], out sv) && int.TryParse(p[4], out sh))))
            {
                vertical.Value = Math.Max(vertical.Minimum, Math.Min(vertical.Maximum, v));
                horizontal.Value = Math.Max(horizontal.Minimum, Math.Min(horizontal.Maximum, h));
                secondaryVertical.Value = Math.Max(secondaryVertical.Minimum, Math.Min(secondaryVertical.Maximum, sv));
                secondaryHorizontal.Value = Math.Max(secondaryHorizontal.Minimum, Math.Min(secondaryHorizontal.Maximum, sh));
                interval.Value = Math.Max(interval.Minimum, Math.Min(interval.Maximum, ms));
                requireRight.Checked = p[rightIndex] == "1";
                presetName.Text = wanted;
                break;
            }
        }
    }
    System.Collections.Generic.List<string> ReadPresets()
    {
        try { return File.Exists(PresetFile) ? new System.Collections.Generic.List<string>(File.ReadAllLines(PresetFile)) : new System.Collections.Generic.List<string>(); }
        catch { return new System.Collections.Generic.List<string>(); }
    }
    string PresetName(string line)
    {
        int split = line.IndexOf('|');
        try { return Uri.UnescapeDataString(split < 0 ? line : line.Substring(0, split)); } catch { return ""; }
    }
    void RefreshPresets(string selected)
    {
        presets.Items.Clear();
        foreach (string line in ReadPresets()) { string name = PresetName(line); if (name.Length > 0) presets.Items.Add(name); }
        if (selected != null) {
            for (int i = 0; i < presets.Items.Count; i++) if (presets.Items[i].ToString().Equals(selected, StringComparison.OrdinalIgnoreCase)) { presets.SelectedIndex = i; break; }
            presetName.Text = selected;
        } else presets.SelectedIndex = -1;
    }
    void WritePresets(System.Collections.Generic.List<string> entries)
    {
        string temp = PresetFile + ".tmp";
        File.WriteAllLines(temp, entries.ToArray());
        if (File.Exists(PresetFile)) File.Replace(temp, PresetFile, PresetFile + ".bak");
        else File.Move(temp, PresetFile);
    }
    void UpdateStatus()
    {
        status.Text = active ? "● LIGADO — F1 para desligar" : "○ DESLIGADO — F1 para ligar";
        status.ForeColor = active ? Color.FromArgb(255, 90, 110) : Color.FromArgb(170, 115, 125);
        toggle.Text = active ? "DESLIGAR (F1)" : "LIGAR (F1)";
        toggle.BackColor = active ? Color.FromArgb(115, 30, 43) : Color.FromArgb(205, 45, 65);
    }
    void UpdateWeaponStatus()
    {
        weaponStatus.Text = secondaryActive ? "ARMA ATIVA: SECUNDÁRIA" : "ARMA ATIVA: PRIMÁRIA";
        weaponStatus.ForeColor = secondaryActive ? Color.FromArgb(255, 170, 110) : Color.FromArgb(255, 105, 125);
        toggleWeapon.Text = secondaryActive ? "USAR PRIMÁRIA (F2)" : "USAR SECUNDÁRIA (F2)";
    }
    static bool Down(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }
    static NumericUpDown Number(decimal min, decimal max, decimal value, decimal step) { return new NumericUpDown { Minimum = min, Maximum = max, Value = value, Increment = step, Width = 125, ThousandsSeparator = true }; }
    static TrackBar Slider(int min, int max, int value, int tick) { return new TrackBar { Minimum = min, Maximum = max, Value = value, TickFrequency = tick, TickStyle = TickStyle.None, Dock = DockStyle.Fill, Height = 34 }; }
    static Label SectionLabel(string text) { return new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI Semibold", 10), ForeColor = Color.FromArgb(235, 75, 95), Padding = new Padding(0, 8, 0, 2) }; }
    static Button AccentButton(string text, Color color) { return new Button { Text = text, AutoSize = true, Height = 34, Padding = new Padding(10, 3, 10, 3), FlatStyle = FlatStyle.Flat, BackColor = color, ForeColor = Color.White, Cursor = Cursors.Hand }; }
    static void ApplyTheme(Control root)
    {
        foreach (Control c in root.Controls) {
            if (c is TextBox || c is ComboBox || c is NumericUpDown) { c.BackColor = Color.FromArgb(55, 31, 37); c.ForeColor = Color.White; }
            else if (!(c is Button) && !(c is TrackBar) && !(c is Label)) { if (c.BackColor == SystemColors.Control) c.BackColor = root.BackColor; c.ForeColor = root.ForeColor; }
            ApplyTheme(c);
        }
    }
    static Image LoadEmbeddedImage(string resourceName)
    {
        try {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)) {
                if (stream == null) return null;
                using (Image original = Image.FromStream(stream)) return new Bitmap(original);
            }
        } catch { return null; }
    }
    static void Row(TableLayoutPanel p, string label, Control c, int row) { p.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row); p.Controls.Add(c, 1, row); }

    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
}
