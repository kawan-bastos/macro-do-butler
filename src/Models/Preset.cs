internal sealed class Preset
{
    public string Name { get; set; }
    public int PrimaryVertical { get; set; }
    public int PrimaryHorizontal { get; set; }
    public int SecondaryVertical { get; set; }
    public int SecondaryHorizontal { get; set; }
    public int IntervalMilliseconds { get; set; }
    public bool RequireRightButton { get; set; }
}
