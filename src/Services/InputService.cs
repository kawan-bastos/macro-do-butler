using System;
using System.Diagnostics;
using System.Threading;

internal sealed class InputService : IDisposable
{
    private readonly Func<MovementSettings> settingsProvider;
    private readonly Thread worker;
    private volatile bool stopping;
    private volatile bool fallbackF12;
    internal event Action ToggleRequested;
    internal event Action WeaponToggleRequested;
    internal event Action ExitRequested;

    internal InputService(Func<MovementSettings> provider)
    {
        settingsProvider = provider;
        worker = new Thread(Run) { IsBackground = true, Name = "Movimento" };
    }
    internal void Start() { worker.Start(); }
    internal void EnableFallbackF12(bool enabled) { fallbackF12 = enabled; }
    internal void RaiseToggleRequested() { Raise(ToggleRequested); }
    internal void RaiseWeaponToggleRequested() { Raise(WeaponToggleRequested); }
    public void Dispose() { stopping = true; }

    private void Run()
    {
        double remainderX = 0, remainderY = 0;
        var watch = Stopwatch.StartNew(); long previousTick = watch.ElapsedTicks;
        bool oldF12=false;
        while (!stopping)
        {
            MovementSettings settings = SafeSettings();
            Thread.Sleep(settings.IntervalMilliseconds);
            bool f12=fallbackF12&&NativeMethods.IsKeyDown(NativeMethods.F12);
            if(f12&&!oldF12)Raise(ExitRequested);
            oldF12=f12;
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
    private static void Raise(Action action) { try { if (action != null) action(); } catch { } }
}
