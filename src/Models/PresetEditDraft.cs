using System;
using System.ComponentModel;

// A detached edit buffer. Changes here never reach the running macro or storage before Save.
internal sealed class PresetEditDraft : INotifyPropertyChanged
{
    private readonly Preset value;
    private readonly Preset baseline;
    internal PresetEditDraft(Preset preset, bool isNew = false)
    {
        MinimumInterval = isNew ? 0 : 2;
        value = PresetEditingService.Copy(preset);
        if (!isNew) value.IntervalMilliseconds = value.IntervalMilliseconds >= 2 && value.IntervalMilliseconds <= 50 ? value.IntervalMilliseconds : 7;
        value.SecondaryIntervalMilliseconds = value.SecondaryIntervalMilliseconds ?? value.IntervalMilliseconds;
        baseline = PresetEditingService.Copy(value);
    }
    public int MinimumInterval { get; private set; }
    public event PropertyChangedEventHandler PropertyChanged;
    public string GameName { get { return value.GameName; } }
    public string Name { get { return value.Name; } set { this.value.Name = value; Changed("Name"); } }
    public int PrimaryVertical { get { return value.PrimaryVertical; } set { Check(value, 0, 10000); this.value.PrimaryVertical = value; Changed("PrimaryVertical"); } }
    public int PrimaryHorizontal { get { return value.PrimaryHorizontal; } set { Check(value, -5000, 5000); this.value.PrimaryHorizontal = value; Changed("PrimaryHorizontal"); } }
    public int SecondaryVertical { get { return value.SecondaryVertical; } set { Check(value, 0, 10000); this.value.SecondaryVertical = value; Changed("SecondaryVertical"); } }
    public int SecondaryHorizontal { get { return value.SecondaryHorizontal; } set { Check(value, -5000, 5000); this.value.SecondaryHorizontal = value; Changed("SecondaryHorizontal"); } }
    public int PrimaryInterval { get { return value.IntervalMilliseconds; } set { Check(value, MinimumInterval, 50); this.value.IntervalMilliseconds = value; Changed("PrimaryInterval"); } }
    public int SecondaryInterval { get { return value.SecondaryIntervalMilliseconds.Value; } set { Check(value, MinimumInterval, 50); this.value.SecondaryIntervalMilliseconds = value; Changed("SecondaryInterval"); } }
    public bool RequireRight { get { return value.RequireRightButton; } set { this.value.RequireRightButton = value; Changed("RequireRight"); } }
    internal bool IsDirty { get { return !PresetEditingService.SameContent(value, baseline); } }
    internal Preset ToPreset() { var result = PresetEditingService.Copy(value); result.Name = (result.Name ?? string.Empty).Trim(); return result; }
    internal MovementSettings ToMovementSettings(bool enabled, bool secondaryActive)
    {
        return new MovementSettings
        {
            Enabled = enabled,
            RequireRightButton = value.RequireRightButton,
            HorizontalPixelsPerSecond = secondaryActive ? value.SecondaryHorizontal : value.PrimaryHorizontal,
            VerticalPixelsPerSecond = secondaryActive ? value.SecondaryVertical : value.PrimaryVertical,
            IntervalMilliseconds = Math.Max(2, secondaryActive ? value.SecondaryIntervalMilliseconds.Value : value.IntervalMilliseconds)
        };
    }
    private static void Check(int number, int min, int max) { if (number < min || number > max) throw new ArgumentOutOfRangeException("value", "Use um valor entre " + min + " e " + max + "."); }
    private void Changed(string name) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
}
