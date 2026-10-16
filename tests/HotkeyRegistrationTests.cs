using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class FakeKeyboardState : IKeyboardStateReader
{
    internal readonly HashSet<int> Down=new HashSet<int>();
    public bool IsDown(int virtualKey){return Down.Contains(virtualKey);}
}

internal static class HotkeyRegistrationTests
{
    private static int checks;
    private static void Assert(bool value,string message){if(!value)throw new Exception(message);checks++;}
    private static bool Has(IList<string> actions,string action){return actions.Contains(action);}
    private static void Main()
    {
        var state=new FakeKeyboardState();
        var poller=new GlobalKeyboardShortcutPoller(state);
        poller.SetShortcuts(new[]{new GlobalKeyboardShortcut("ToggleMacro",0x70,0),new GlobalKeyboardShortcut("SwitchWeapon",0x77,2)});
        state.Down.Add(0x70);
        Assert(Has(poller.PollOnce(),"ToggleMacro"),"A key press was not detected without window messages");
        Assert(poller.PollOnce().Count==0,"A held key repeated its action");
        state.Down.Clear();poller.PollOnce();state.Down.Add(0x11);state.Down.Add(0x77);
        Assert(Has(poller.PollOnce(),"SwitchWeapon"),"A configured Ctrl shortcut was not detected globally");
        Assert(poller.PollOnce().Count==0,"A held chord repeated its action");
        state.Down.Remove(0x11);state.Down.Add(0x77);poller.SetShortcuts(new[]{new GlobalKeyboardShortcut("SwitchWeapon",0x77,2)});
        Assert(poller.PollOnce().Count==0,"Changing bindings fired a key that was already held");
        state.Down.Clear();poller.PollOnce();state.Down.Add(0x11);state.Down.Add(0x77);
        Assert(Has(poller.PollOnce(),"SwitchWeapon"),"Shortcut did not work after changing configuration");
        poller.SetShortcuts(new GlobalKeyboardShortcut[0]);
        Assert(poller.PollOnce().Count==0,"Removed keyboard handlers remained active");
        poller.SetShortcuts(new[]{new GlobalKeyboardShortcut("ToggleMacro",0x70,0)});poller.Dispose();state.Down.Clear();state.Down.Add(0x70);
        Assert(poller.PollOnce().Count==0,"Disposed keyboard polling remained active");
        Console.WriteLine("PASS: "+checks+" checks.");
    }
}
