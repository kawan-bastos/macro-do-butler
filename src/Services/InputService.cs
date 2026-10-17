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
        var session = new MovementSession();
        var watch = Stopwatch.StartNew(); long previousTick = watch.ElapsedTicks;
        while (!stopping)
        {
            MovementSettings settings = SafeSettings();
            Thread.Sleep(Math.Max(2, Math.Min(50, settings.IntervalMilliseconds)));
            long currentTick = watch.ElapsedTicks;
            double elapsed = (currentTick - previousTick) / (double)Stopwatch.Frequency;
            previousTick = currentTick;
            var delta=session.Step(settings, NativeMethods.IsKeyDown(NativeMethods.LeftMouseButton), NativeMethods.IsKeyDown(NativeMethods.RightMouseButton), elapsed);
            int x=(int)delta.Horizontal, y=(int)delta.Vertical;
            if(x!=0||y!=0)NativeMethods.MoveMouse(x,y);
        }
    }

    private MovementSettings SafeSettings() { try { return settingsProvider(); } catch { return new MovementSettings { IntervalMilliseconds = 7, RequireRightButton = true }; } }
}
