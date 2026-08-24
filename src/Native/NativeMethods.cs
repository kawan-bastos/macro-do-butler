using System;
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    internal const int LeftMouseButton = 0x01;
    internal const int RightMouseButton = 0x02;
    internal const int F1 = 0x70;
    internal const int F2 = 0x71;
    internal const int F12 = 0x7B;
    private const uint MouseEventMove = 0x0001;

    internal static bool IsKeyDown(int virtualKey) { return (GetAsyncKeyState(virtualKey) & 0x8000) != 0; }
    internal static void MoveMouse(int x, int y) { mouse_event(MouseEventMove, x, y, 0, UIntPtr.Zero); }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extraInfo);
}
