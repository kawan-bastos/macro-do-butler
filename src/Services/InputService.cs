using System;
using System.Diagnostics;
using System.Threading;

internal sealed class InputService : IDisposable
{
    private readonly Func<MovementSettings> settingsProvider;
    private readonly Thread worker;
    private volatile bool stopping;
    internal InputService(Func<MovementSettings> provider)
    {
        settingsProvider = provider;
        worker = new Thread(Run) { IsBackground = true, Name = "Movimento" };
    }
    internal void Start() { worker.Start(); }
    public void Dispose() { stopping = true; }

    private void Run()
    {
        double remainderX = 0, remainderY = 0;
        var watch = Stopwatch.StartNew(); long previousTick = watch.ElapsedTicks;
        while (!stopping)
        {
            MovementSettings settings = SafeSettings();
            Thread.Sleep(settings.IntervalMilliseconds);
            long currentTick = watch.ElapsedTicks;
            double elapsed = (currentTick - previousTick) / (double)Stopwatch.Frequency;
            previousTick = currentTick;
            bool moving = settings.Enabled && NativeMethods.IsKeyDown(NativeMethods.LeftMouseButton) && (!settings.RequireRightButton || NativeMethods.IsKeyDown(NativeMethods.RightMouseButton));
            if (!moving) { remainderX = remainderY = 0; continue; }
            remainderX += settings.HorizontalPixelsPerSecond * elapsed;
            remainderY += settings.VerticalPixelsPerSecond * elapsed;
            int x = (int)Math.Truncate(remainderX), y = (int)Math.Truncate(remainderY);
            remainderX -= x; remainderY -= y;
            if (x != 0 || y != 0) NativeMethods.MoveMouse(x, y);
        }
    }

    private MovementSettings SafeSettings() { try { return settingsProvider(); } catch { return new MovementSettings { IntervalMilliseconds = 7, RequireRightButton = true }; } }
}
