using System;
using System.Drawing;
using System.Windows.Forms;

internal sealed class MainForm : Form
{
    private readonly NumericUpDown primaryVertical = Theme.Number(0, 10000, 350, 25);
    private readonly NumericUpDown primaryHorizontal = Theme.Number(-5000, 5000, 0, 25);
    private readonly NumericUpDown secondaryVertical = Theme.Number(0, 10000, 0, 25);
    private readonly NumericUpDown secondaryHorizontal = Theme.Number(-5000, 5000, 0, 25);
    private readonly NumericUpDown interval = Theme.Number(2, 50, 7, 1);
    private readonly TrackBar primaryVerticalSlider = Theme.Slider(0, 10000, 350, 500);
    private readonly TrackBar primaryHorizontalSlider = Theme.Slider(-5000, 5000, 0, 500);
    private readonly TrackBar secondaryVerticalSlider = Theme.Slider(0, 10000, 0, 500);
    private readonly TrackBar secondaryHorizontalSlider = Theme.Slider(-5000, 5000, 0, 500);
    private readonly TrackBar intervalSlider = Theme.Slider(2, 50, 7, 4);
    private readonly CheckBox requireRight = new CheckBox { Text = "Exigir botão direito pressionado", Checked = true, AutoSize = true };
    private readonly ComboBox presetList = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly TextBox presetName = new TextBox { Width = 230 };
    private readonly Label status = new Label { Height = 42, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 11) };
    private readonly Label weaponStatus = new Label { Height = 30, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 10) };
    private readonly Button toggleButton = Theme.AccentButton("LIGAR (F1)", Theme.Accent);
    private readonly Button weaponButton = Theme.AccentButton("TROCAR ARMA (F2)", Color.FromArgb(155, 42, 62));
    private readonly PresetService presetService;
    private readonly InputService inputService;
    private readonly NotifyIcon tray;
    private volatile bool enabled;
    private volatile bool secondaryActive;

    internal MainForm()
    {
        presetService = new PresetService(AppDomain.CurrentDomain.BaseDirectory);
        inputService = new InputService(GetMovementSettings);
        Text = "Macro Do Butler";
        ClientSize = new Size(650, 810);
        MinimumSize = new Size(666, 849);
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        BackColor = Theme.Background;
        ForeColor = Theme.Foreground;

        BuildInterface();
        tray = new NotifyIcon { Visible = true, Text = "Macro Do Butler", Icon = Icon };
        BindEvents();
        RefreshPresets(null);
        UpdateStatus();
        UpdateWeaponStatus();
        Theme.Apply(this);
        inputService.Start();
    }

    private void BuildInterface()
    {
        Button saveButton = Theme.AccentButton("SALVAR PRESET", Theme.Accent);
        Button deleteButton = Theme.AccentButton("EXCLUIR", Color.FromArgb(120, 35, 48));
        saveButton.Click += delegate { SavePreset(); };
        deleteButton.Click += delegate { DeletePreset(); };
        var presetButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        presetButtons.Controls.Add(saveButton); presetButtons.Controls.Add(deleteButton);

        var header = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Theme.Surface, Padding = new Padding(18, 10, 22, 8) };
        Image brand = Theme.LoadEmbeddedImage("MacroDoButler.Brand");
        if (brand != null) header.Controls.Add(new PictureBox { Image = brand, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(72, 72), Location = new Point(14, 11), BackColor = Color.Transparent });
        header.Controls.Add(new Label { Text = "MACRO DO BUTLER", Font = new Font("Segoe UI Semibold", 17), ForeColor = Color.White, AutoSize = true, Location = new Point(98, 18) });
        header.Controls.Add(new Label { Text = "Presets persistentes • ajuste em tempo real", ForeColor = Color.FromArgb(205, 150, 160), AutoSize = true, Location = new Point(101, 54) });

        var grid = new TableLayoutPanel { ColumnCount = 2, RowCount = 18, Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 10), BackColor = Theme.Background };
        grid.BackgroundImage = Theme.LoadEmbeddedImage("MacroDoButler.Watermark");
        grid.BackgroundImageLayout = ImageLayout.Zoom;
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        AddRow(grid, "Carregar preset", presetList, 0);
        AddRow(grid, "Nome para salvar/atualizar", presetName, 1);
        grid.Controls.Add(presetButtons, 1, 2);
        AddFullRow(grid, Theme.SectionLabel("ARMA PRIMÁRIA"), 3);
        AddRow(grid, "Força vertical (0–10.000 px/s)", primaryVertical, 4);
        AddFullRow(grid, primaryVerticalSlider, 5);
        AddRow(grid, "Correção lateral (-5.000 a 5.000 px/s)", primaryHorizontal, 6);
        AddFullRow(grid, primaryHorizontalSlider, 7);
        AddFullRow(grid, Theme.SectionLabel("ARMA SECUNDÁRIA"), 8);
        AddRow(grid, "Força vertical secundária", secondaryVertical, 9);
        AddFullRow(grid, secondaryVerticalSlider, 10);
        AddRow(grid, "Correção lateral secundária", secondaryHorizontal, 11);
        AddFullRow(grid, secondaryHorizontalSlider, 12);
        AddRow(grid, "Intervalo compartilhado (2–50 ms)", interval, 13);
        AddFullRow(grid, intervalSlider, 14);
        AddFullRow(grid, requireRight, 15);
        AddFullRow(grid, new Label { Text = "F1 liga/desliga  •  F2 alterna primária/secundária  •  F12 encerra", AutoSize = true, MaximumSize = new Size(580, 0), ForeColor = Color.FromArgb(190, 140, 150) }, 16);

        toggleButton.Height = 46; toggleButton.Dock = DockStyle.Top;
        weaponButton.Height = 38; weaponButton.Dock = DockStyle.Top;
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 176, Padding = new Padding(24, 8, 24, 14), BackColor = Theme.Surface };
        bottom.Controls.Add(toggleButton); bottom.Controls.Add(status); bottom.Controls.Add(weaponButton); bottom.Controls.Add(weaponStatus);
        Controls.Add(grid); Controls.Add(header); Controls.Add(bottom);
    }

    private void BindEvents()
    {
        Bind(primaryVertical, primaryVerticalSlider); Bind(primaryHorizontal, primaryHorizontalSlider);
        Bind(secondaryVertical, secondaryVerticalSlider); Bind(secondaryHorizontal, secondaryHorizontalSlider); Bind(interval, intervalSlider);
        toggleButton.Click += delegate { Toggle(); };
        weaponButton.Click += delegate { ToggleWeapon(); };
        presetList.SelectionChangeCommitted += delegate { LoadSelectedPreset(); };
        tray.DoubleClick += delegate { Show(); WindowState = FormWindowState.Normal; Activate(); };
        Resize += delegate { if (WindowState == FormWindowState.Minimized) Hide(); };
        FormClosed += delegate { inputService.Dispose(); tray.Dispose(); };
        inputService.ToggleRequested += delegate { InvokeOnUi(Toggle); };
        inputService.WeaponToggleRequested += delegate { InvokeOnUi(ToggleWeapon); };
        inputService.ExitRequested += delegate { InvokeOnUi(Close); };
    }

    private MovementSettings GetMovementSettings()
    {
        return new MovementSettings
        {
            Enabled = enabled,
            RequireRightButton = ReadCheck(requireRight, true),
            HorizontalPixelsPerSecond = ReadNumber(secondaryActive ? secondaryHorizontal : primaryHorizontal, 0),
            VerticalPixelsPerSecond = ReadNumber(secondaryActive ? secondaryVertical : primaryVertical, secondaryActive ? 0 : 350),
            IntervalMilliseconds = ReadNumber(interval, 7)
        };
    }

    private void SavePreset()
    {
        string name = presetName.Text.Trim();
        if (name.Length == 0) { MessageBox.Show("Digite um nome para o preset.", "Preset", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        try
        {
            presetService.Save(new Preset { Name = name, PrimaryVertical = (int)primaryVertical.Value, PrimaryHorizontal = (int)primaryHorizontal.Value, SecondaryVertical = (int)secondaryVertical.Value, SecondaryHorizontal = (int)secondaryHorizontal.Value, IntervalMilliseconds = (int)interval.Value, RequireRightButton = requireRight.Checked });
            RefreshPresets(name);
            MessageBox.Show("Preset '" + name + "' salvo sem alterar os outros.", "Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show("Não foi possível salvar:\r\n" + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void DeletePreset()
    {
        string name = presetList.SelectedItem == null ? presetName.Text.Trim() : presetList.SelectedItem.ToString();
        if (name.Length == 0) return;
        try { presetService.Delete(name); RefreshPresets(null); presetName.Text = ""; }
        catch (Exception ex) { MessageBox.Show("Não foi possível excluir:\r\n" + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void LoadSelectedPreset()
    {
        foreach (Preset preset in presetService.LoadAll())
        {
            if (!preset.Name.Equals(presetList.Text, StringComparison.OrdinalIgnoreCase)) continue;
            primaryVertical.Value = Clamp(primaryVertical, preset.PrimaryVertical);
            primaryHorizontal.Value = Clamp(primaryHorizontal, preset.PrimaryHorizontal);
            secondaryVertical.Value = Clamp(secondaryVertical, preset.SecondaryVertical);
            secondaryHorizontal.Value = Clamp(secondaryHorizontal, preset.SecondaryHorizontal);
            interval.Value = Clamp(interval, preset.IntervalMilliseconds);
            requireRight.Checked = preset.RequireRightButton;
            presetName.Text = preset.Name;
            break;
        }
    }

    private void RefreshPresets(string selected)
    {
        presetList.Items.Clear();
        foreach (Preset preset in presetService.LoadAll()) presetList.Items.Add(preset.Name);
        presetList.SelectedIndex = -1;
        if (selected == null) return;
        for (int i = 0; i < presetList.Items.Count; i++) if (presetList.Items[i].ToString().Equals(selected, StringComparison.OrdinalIgnoreCase)) { presetList.SelectedIndex = i; break; }
        presetName.Text = selected;
    }

    private void Toggle() { enabled = !enabled; UpdateStatus(); }
    private void ToggleWeapon() { secondaryActive = !secondaryActive; UpdateWeaponStatus(); }
    private void UpdateStatus()
    {
        status.Text = enabled ? "● LIGADO — F1 para desligar" : "○ DESLIGADO — F1 para ligar";
        status.ForeColor = enabled ? Color.FromArgb(255, 90, 110) : Color.FromArgb(170, 115, 125);
        toggleButton.Text = enabled ? "DESLIGAR (F1)" : "LIGAR (F1)";
        toggleButton.BackColor = enabled ? Color.FromArgb(115, 30, 43) : Theme.Accent;
    }
    private void UpdateWeaponStatus()
    {
        weaponStatus.Text = secondaryActive ? "ARMA ATIVA: SECUNDÁRIA" : "ARMA ATIVA: PRIMÁRIA";
        weaponStatus.ForeColor = secondaryActive ? Color.FromArgb(255, 170, 110) : Color.FromArgb(255, 105, 125);
        weaponButton.Text = secondaryActive ? "USAR PRIMÁRIA (F2)" : "USAR SECUNDÁRIA (F2)";
    }

    private void InvokeOnUi(Action action) { try { if (!IsDisposed) BeginInvoke(action); } catch { } }
    private static int ReadNumber(NumericUpDown c, int fallback) { try { return c.InvokeRequired ? (int)c.Invoke(new Func<int>(delegate { return (int)c.Value; })) : (int)c.Value; } catch { return fallback; } }
    private static bool ReadCheck(CheckBox c, bool fallback) { try { return c.InvokeRequired ? (bool)c.Invoke(new Func<bool>(delegate { return c.Checked; })) : c.Checked; } catch { return fallback; } }
    private static decimal Clamp(NumericUpDown c, int value) { return Math.Max(c.Minimum, Math.Min(c.Maximum, value)); }
    private static void Bind(NumericUpDown n, TrackBar s) { n.ValueChanged += delegate { if (s.Value != (int)n.Value) s.Value = (int)n.Value; }; s.ValueChanged += delegate { if (n.Value != s.Value) n.Value = s.Value; }; }
    private static void AddRow(TableLayoutPanel g, string label, Control c, int row) { g.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row); g.Controls.Add(c, 1, row); }
    private static void AddFullRow(TableLayoutPanel g, Control c, int row) { g.Controls.Add(c, 0, row); g.SetColumnSpan(c, 2); }
}
