using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

internal sealed class HotkeyService : IDisposable
{
    private readonly IntPtr hwnd; private readonly HwndSource source; private readonly Dictionary<int,string> actions=new Dictionary<int,string>(); private readonly Dictionary<string,int> ids=new Dictionary<string,int>(); private int nextId=0x4B00;
    internal event Action<string> ActionRequested;
    internal string LastRegistrationWarning { get; private set; }
    internal HotkeyService(IntPtr handle) { hwnd=handle; source=HwndSource.FromHwnd(hwnd); source.AddHook(WndProc); }
    internal string Register(HotkeySettings s)
    {
        UnregisterAll(); LastRegistrationWarning=RegisterAvailable(s); return LastRegistrationWarning;
    }
    internal string TryReplace(HotkeySettings previous, HotkeySettings candidate)
    {
        UnregisterAll();
        LastRegistrationWarning=RegisterAvailable(candidate);
        string[] names={"ToggleMacro","SwitchWeapon","ExitApplication","ShowWindow"};
        string[] oldValues={previous.ToggleMacro,previous.SwitchWeapon,previous.ExitApplication,previous.ShowWindow};
        string[] newValues={candidate.ToggleMacro,candidate.SwitchWeapon,candidate.ExitApplication,candidate.ShowWindow};
        bool changed=false; string failedAction=null;
        for(int i=0;i<names.Length;i++) if(!string.Equals(oldValues[i],newValues[i],StringComparison.Ordinal)) { changed=true; if(!string.IsNullOrWhiteSpace(newValues[i])&&!ids.ContainsKey(names[i])) failedAction=names[i]; }
        if(changed&&failedAction!=null)
        {
            string failure=LastRegistrationWarning;
            UnregisterAll(); Register(previous);
            return failure??"O novo atalho não pôde ser registrado. A configuração anterior foi restaurada.";
        }
        return null;
    }
    private string RegisterAvailable(HotkeySettings settings)
    {
        string firstError=null; string[] names={"ToggleMacro","SwitchWeapon","ExitApplication","ShowWindow"}; string[] labels={"Ligar/desligar macro","Alternar arma","Fechar aplicativo","Mostrar janela"}; string[] values={settings.ToggleMacro,settings.SwitchWeapon,settings.ExitApplication,settings.ShowWindow};
        for(int i=0;i<values.Length;i++)
        {
            if(string.IsNullOrWhiteSpace(values[i]))continue; HotkeyBinding binding;
            if(!HotkeyParser.TryParse(values[i],out binding)){if(firstError==null)firstError=labels[i]+": combinação inválida (“"+values[i]+"”).";continue;}
            // Windows reserves F12 for debuggers; preserve the historical global polling fallback.
            if(i==2&&binding.Key==Key.F12) continue;
            int id=++nextId;
            if(RegisterHotKey(hwnd,id,(uint)binding.Modifiers|0x4000,(uint)binding.VirtualKey)){actions[id]=names[i];ids[names[i]]=id;}
            else if(firstError==null){int error=Marshal.GetLastWin32Error();firstError=labels[i]+": o Windows não conseguiu registrar "+values[i]+" (código "+error+"). Pode estar reservado ou em uso.";}
        }
        return firstError;
    }
    internal void UnregisterAll() { foreach(int id in new List<int>(actions.Keys)) UnregisterHotKey(hwnd,id); actions.Clear(); ids.Clear(); }
    private IntPtr WndProc(IntPtr handle,int message,IntPtr wParam,IntPtr lParam,ref bool handled) { if(message==0x0312){string name; if(actions.TryGetValue(wParam.ToInt32(),out name)){handled=true; var callback=ActionRequested; if(callback!=null)callback(name);}} return IntPtr.Zero; }
    public void Dispose(){UnregisterAll(); if(source!=null)source.RemoveHook(WndProc);}
    [DllImport("user32.dll",SetLastError=true)] private static extern bool RegisterHotKey(IntPtr hWnd,int id,uint modifiers,uint virtualKey);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool UnregisterHotKey(IntPtr hWnd,int id);
}
