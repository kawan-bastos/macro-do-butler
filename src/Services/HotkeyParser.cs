using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;

internal sealed class HotkeyBinding
{
    internal ModifierKeys Modifiers { get; private set; }
    internal Key Key { get; private set; }
    internal string MouseButton { get; private set; }
    internal bool IsMouseButton { get { return !string.IsNullOrEmpty(MouseButton); } }
    internal int VirtualKey { get { return IsMouseButton ? 0 : KeyInterop.VirtualKeyFromKey(Key); } }
    internal HotkeyBinding(ModifierKeys modifiers, Key key) { Modifiers=modifiers; Key=key; }
    internal HotkeyBinding(ModifierKeys modifiers, string mouseButton) { Modifiers=modifiers; MouseButton=mouseButton; Key=Key.None; }
    public override bool Equals(object obj)
    {
        var other=obj as HotkeyBinding;
        return other!=null&&other.Modifiers==Modifiers&&other.IsMouseButton==IsMouseButton&&
            (IsMouseButton?string.Equals(MouseButton,other.MouseButton,StringComparison.OrdinalIgnoreCase):other.Key==Key);
    }
    public override int GetHashCode() { return ((int)Modifiers*397)^(IsMouseButton?StringComparer.OrdinalIgnoreCase.GetHashCode(MouseButton):(int)Key); }
}

internal static class HotkeyParser
{
    internal static bool TryParse(string text, out HotkeyBinding binding)
    {
        binding=null; if(string.IsNullOrWhiteSpace(text)) return false;
        ModifierKeys modifiers=ModifierKeys.None; Key key=Key.None; string mouseButton=null;
        string[] parts=text.Split('+').Select(p=>p.Trim()).ToArray();
        foreach(string part in parts)
        {
            if(part.Equals("Ctrl",StringComparison.OrdinalIgnoreCase)||part.Equals("Control",StringComparison.OrdinalIgnoreCase)) modifiers|=ModifierKeys.Control;
            else if(part.Equals("Shift",StringComparison.OrdinalIgnoreCase)) modifiers|=ModifierKeys.Shift;
            else if(part.Equals("Alt",StringComparison.OrdinalIgnoreCase)) modifiers|=ModifierKeys.Alt;
            else if(part.Equals("Win",StringComparison.OrdinalIgnoreCase)||part.Equals("Windows",StringComparison.OrdinalIgnoreCase)) modifiers|=ModifierKeys.Windows;
            else if(TryParseMouseButton(part,out mouseButton)) { }
            else if(!Enum.TryParse(part,true,out key)||key==Key.None) return false;
        }
        if(mouseButton!=null)
        {
            if(key!=Key.None) return false;
            binding=new HotkeyBinding(modifiers,mouseButton); return true;
        }
        if(key==Key.None||!HotkeyValidator.IsAllowed(key)) return false;
        binding=new HotkeyBinding(modifiers,key); return true;
    }
    internal static bool TryParseMouseButton(string value,out string mouseButton)
    {
        mouseButton=null;
        if(value.Equals("Mouse Left",StringComparison.OrdinalIgnoreCase)||value.Equals("MouseLeft",StringComparison.OrdinalIgnoreCase)||value.Equals("Left Mouse Button",StringComparison.OrdinalIgnoreCase)||value.Equals("Mouse esquerdo",StringComparison.OrdinalIgnoreCase)) mouseButton="MouseLeft";
        else if(value.Equals("Mouse Right",StringComparison.OrdinalIgnoreCase)||value.Equals("MouseRight",StringComparison.OrdinalIgnoreCase)||value.Equals("Right Mouse Button",StringComparison.OrdinalIgnoreCase)||value.Equals("Mouse direito",StringComparison.OrdinalIgnoreCase)) mouseButton="MouseRight";
        else if(value.Equals("Mouse Middle",StringComparison.OrdinalIgnoreCase)||value.Equals("MouseMiddle",StringComparison.OrdinalIgnoreCase)||value.Equals("Middle Mouse Button",StringComparison.OrdinalIgnoreCase)||value.Equals("Mouse 3",StringComparison.OrdinalIgnoreCase)) mouseButton="MouseMiddle";
        else if(value.Equals("Mouse 4",StringComparison.OrdinalIgnoreCase)||value.Equals("Mouse4",StringComparison.OrdinalIgnoreCase)||value.Equals("Mouse X1",StringComparison.OrdinalIgnoreCase)||value.Equals("MouseX1",StringComparison.OrdinalIgnoreCase)) mouseButton="MouseX1";
        else if(value.Equals("Mouse 5",StringComparison.OrdinalIgnoreCase)||value.Equals("Mouse5",StringComparison.OrdinalIgnoreCase)||value.Equals("Mouse X2",StringComparison.OrdinalIgnoreCase)||value.Equals("MouseX2",StringComparison.OrdinalIgnoreCase)) mouseButton="MouseX2";
        return mouseButton!=null;
    }
    internal static bool TryMouseButtonFromEvent(MouseButtonEventArgs e,out string mouseButton)
    {
        switch(e.ChangedButton)
        {
            case MouseButton.Left: mouseButton="MouseLeft"; return true;
            case MouseButton.Right: mouseButton="MouseRight"; return true;
            case MouseButton.Middle: mouseButton="MouseMiddle"; return true;
            case MouseButton.XButton1: mouseButton="MouseX1"; return true;
            case MouseButton.XButton2: mouseButton="MouseX2"; return true;
            default: mouseButton=null; return false;
        }
    }
    internal static string Format(ModifierKeys modifiers, Key key)
    {
        return FormatModifiers(modifiers)+key.ToString();
    }
    internal static string Format(ModifierKeys modifiers,string mouseButton)
    {
        string label=mouseButton=="MouseLeft"?"Mouse esquerdo":mouseButton=="MouseRight"?"Mouse direito":mouseButton=="MouseMiddle"?"Mouse 3":mouseButton=="MouseX1"?"Mouse 4":"Mouse 5";
        return FormatModifiers(modifiers)+label;
    }
    private static string FormatModifiers(ModifierKeys modifiers)
    {
        string result=""; if((modifiers&ModifierKeys.Control)!=0)result+="Ctrl + "; if((modifiers&ModifierKeys.Shift)!=0)result+="Shift + "; if((modifiers&ModifierKeys.Alt)!=0)result+="Alt + "; if((modifiers&ModifierKeys.Windows)!=0)result+="Win + "; return result;
    }
    internal static bool TryKeyFromEvent(KeyEventArgs e, out Key key) { key=e.Key==Key.System?e.SystemKey:e.Key; if(key==Key.LeftCtrl||key==Key.RightCtrl||key==Key.LeftShift||key==Key.RightShift||key==Key.LeftAlt||key==Key.RightAlt||key==Key.LWin||key==Key.RWin) return false; return key!=Key.None; }
    internal static ModifierKeys CurrentModifiers()
    {
        ModifierKeys m=ModifierKeys.None;
        if(IsKeyDown(0xA2)||IsKeyDown(0xA3))m|=ModifierKeys.Control;
        if(IsKeyDown(0xA0)||IsKeyDown(0xA1))m|=ModifierKeys.Shift;
        if(IsKeyDown(0xA4)||IsKeyDown(0xA5))m|=ModifierKeys.Alt;
        if(IsKeyDown(0x5B)||IsKeyDown(0x5C))m|=ModifierKeys.Windows;
        return m;
    }
    private static bool IsKeyDown(int virtualKey) { return (GetAsyncKeyState(virtualKey)&0x8000)!=0; }
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
}

internal static class HotkeyValidator
{
    internal static bool IsAllowed(Key key) { return key!=Key.None&&key!=Key.Escape&&key!=Key.Enter&&key!=Key.Tab&&key!=Key.CapsLock&&key!=Key.NumLock&&key!=Key.Scroll; }
    internal static string Validate(HotkeySettings s)
    {
        string[] names={"Ligar/desligar macro","Alternar arma","Fechar aplicativo","Mostrar janela"};
        string[] values={s.ToggleMacro,s.SwitchWeapon,s.ExitApplication,s.ShowWindow}; HotkeyBinding[] parsed=new HotkeyBinding[values.Length];
        for(int i=0;i<3;i++) if(!HotkeyParser.TryParse(values[i],out parsed[i])) return names[i]+": informe um atalho válido.";
        for(int i=3;i<values.Length;i++) if(!string.IsNullOrWhiteSpace(values[i])&&!HotkeyParser.TryParse(values[i],out parsed[i])) return names[i]+": informe um atalho válido ou deixe desativado.";
        for(int i=0;i<values.Length;i++) for(int j=0;j<i;j++) if(parsed[i]!=null&&parsed[j]!=null&&parsed[i].Equals(parsed[j])) return "Esse atalho já está em uso por “"+names[j]+"”.";
        return null;
    }
}
