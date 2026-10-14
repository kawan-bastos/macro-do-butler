using System;
using System.Linq;
using System.Windows.Input;

internal sealed class HotkeyBinding
{
    internal ModifierKeys Modifiers { get; private set; }
    internal Key Key { get; private set; }
    internal int VirtualKey { get { return KeyInterop.VirtualKeyFromKey(Key); } }
    internal HotkeyBinding(ModifierKeys modifiers, Key key) { Modifiers=modifiers; Key=key; }
    public override bool Equals(object obj) { var other=obj as HotkeyBinding; return other!=null&&other.Modifiers==Modifiers&&other.Key==Key; }
    public override int GetHashCode() { return ((int)Modifiers*397)^(int)Key; }
}

internal static class HotkeyParser
{
    internal static bool TryParse(string text, out HotkeyBinding binding)
    {
        binding=null; if(string.IsNullOrWhiteSpace(text)) return false;
        ModifierKeys modifiers=ModifierKeys.None; Key key=Key.None; string[] parts=text.Split('+').Select(p=>p.Trim()).ToArray();
        foreach(string part in parts)
        {
            if(part.Equals("Ctrl",StringComparison.OrdinalIgnoreCase)||part.Equals("Control",StringComparison.OrdinalIgnoreCase)) modifiers|=ModifierKeys.Control;
            else if(part.Equals("Shift",StringComparison.OrdinalIgnoreCase)) modifiers|=ModifierKeys.Shift;
            else if(part.Equals("Alt",StringComparison.OrdinalIgnoreCase)) modifiers|=ModifierKeys.Alt;
            else if(part.Equals("Win",StringComparison.OrdinalIgnoreCase)||part.Equals("Windows",StringComparison.OrdinalIgnoreCase)) modifiers|=ModifierKeys.Windows;
            else if(!Enum.TryParse(part,true,out key)||key==Key.None) return false;
        }
        if(key==Key.None||!HotkeyValidator.IsAllowed(key)) return false;
        binding=new HotkeyBinding(modifiers,key); return true;
    }
    internal static string Format(ModifierKeys modifiers, Key key)
    {
        string result=""; if((modifiers&ModifierKeys.Control)!=0)result+="Ctrl + "; if((modifiers&ModifierKeys.Shift)!=0)result+="Shift + "; if((modifiers&ModifierKeys.Alt)!=0)result+="Alt + "; if((modifiers&ModifierKeys.Windows)!=0)result+="Win + ";
        return result+key.ToString();
    }
    internal static bool TryKeyFromEvent(KeyEventArgs e, out Key key) { key=e.Key==Key.System?e.SystemKey:e.Key; if(key==Key.LeftCtrl||key==Key.RightCtrl||key==Key.LeftShift||key==Key.RightShift||key==Key.LeftAlt||key==Key.RightAlt||key==Key.LWin||key==Key.RWin) return false; return key!=Key.None; }
    internal static ModifierKeys CurrentModifiers() { ModifierKeys m=ModifierKeys.None; if(Keyboard.IsKeyDown(Key.LeftCtrl)||Keyboard.IsKeyDown(Key.RightCtrl))m|=ModifierKeys.Control; if(Keyboard.IsKeyDown(Key.LeftShift)||Keyboard.IsKeyDown(Key.RightShift))m|=ModifierKeys.Shift; if(Keyboard.IsKeyDown(Key.LeftAlt)||Keyboard.IsKeyDown(Key.RightAlt))m|=ModifierKeys.Alt; if(Keyboard.IsKeyDown(Key.LWin)||Keyboard.IsKeyDown(Key.RWin))m|=ModifierKeys.Windows; return m; }
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
