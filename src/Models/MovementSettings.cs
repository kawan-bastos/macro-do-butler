internal sealed class MovementSettings
{
    public bool Enabled { get; set; }
    public bool RequireRightButton { get; set; }
    public int HorizontalPixelsPerSecond { get; set; }
    public int VerticalPixelsPerSecond { get; set; }
    public int IntervalMilliseconds { get; set; }
}
