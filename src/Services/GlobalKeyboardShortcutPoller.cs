using System;
using System.Collections.Generic;

internal interface IKeyboardStateReader
{
    bool IsDown(int virtualKey);
}

internal sealed class GlobalKeyboardShortcut
{
    internal string Action { get; private set; }
    internal int VirtualKey { get; private set; }
    internal int Modifiers { get; private set; }
    internal GlobalKeyboardShortcut(string action,int virtualKey,int modifiers)
    {
        Action=action; VirtualKey=virtualKey; Modifiers=modifiers;
    }
}

internal sealed class GlobalKeyboardShortcutPoller : IDisposable
{
    private sealed class Entry
    {
        internal GlobalKeyboardShortcut Shortcut;
        internal bool WasDown;
    }
    private const int ModAlt=1, ModControl=2, ModShift=4, ModWindows=8;
    private const int VkAlt=0x12, VkControl=0x11, VkShift=0x10, VkLWin=0x5B, VkRWin=0x5C;
    private readonly IKeyboardStateReader state;
    private readonly object gate=new object();
    private Entry[] entries=new Entry[0];
    private bool disposed;

    internal GlobalKeyboardShortcutPoller(IKeyboardStateReader state)
    {
        this.state=state??throw new ArgumentNullException("state");
    }
    internal void SetShortcuts(IEnumerable<GlobalKeyboardShortcut> shortcuts)
    {
        lock(gate)
        {
            if(disposed)return;
            var next=new List<Entry>();
            foreach(GlobalKeyboardShortcut shortcut in shortcuts)
                next.Add(new Entry{Shortcut=shortcut,WasDown=state.IsDown(shortcut.VirtualKey)});
            entries=next.ToArray();
        }
    }
    internal IList<string> PollOnce()
    {
        var actions=new List<string>();
        lock(gate)
        {
            if(disposed)return actions;
            foreach(Entry entry in entries)
            {
                bool down=state.IsDown(entry.Shortcut.VirtualKey);
                if(down&&!entry.WasDown&&ModifiersMatch(entry.Shortcut.Modifiers))actions.Add(entry.Shortcut.Action);
                entry.WasDown=down;
            }
        }
        return actions;
    }
    private bool ModifiersMatch(int modifiers)
    {
        return state.IsDown(VkAlt)==((modifiers&ModAlt)!=0)
            &&state.IsDown(VkControl)==((modifiers&ModControl)!=0)
            &&state.IsDown(VkShift)==((modifiers&ModShift)!=0)
            &&(state.IsDown(VkLWin)||state.IsDown(VkRWin))==((modifiers&ModWindows)!=0);
    }
    public void Dispose()
    {
        lock(gate){disposed=true;entries=new Entry[0];}
    }
}

internal sealed class Win32KeyboardStateReader : IKeyboardStateReader
{
    public bool IsDown(int virtualKey) { return (GetAsyncKeyState(virtualKey)&0x8000)!=0; }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
