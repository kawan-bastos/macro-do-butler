using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

internal sealed class HotkeyService : IDisposable
{
    private const int WhMouseLl=14;
    private const int WmHotkey=0x0312;
    private readonly IntPtr hwnd;
    private readonly HwndSource source;
    private readonly Dictionary<int,string> actions=new Dictionary<int,string>();
    private readonly Dictionary<string,int> ids=new Dictionary<string,int>();
    private readonly Dictionary<string,string> mouseActions=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    private readonly LowLevelMouseProc mouseProc;
    private IntPtr mouseHook;
    private int nextId=0x4B00;
    internal event Action<string> ActionRequested;
    internal string LastRegistrationWarning { get; private set; }

    internal HotkeyService(IntPtr handle)
    {
        hwnd=handle; source=HwndSource.FromHwnd(hwnd); source.AddHook(WndProc);
        mouseProc=MouseProc;
        IntPtr module=GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName);
        mouseHook=SetWindowsHookEx(WhMouseLl,mouseProc,module,0);
    }
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
            if(string.IsNullOrWhiteSpace(values[i]))continue;
            HotkeyBinding binding;
            if(!HotkeyParser.TryParse(values[i],out binding)){if(firstError==null)firstError=labels[i]+": combinação inválida (“"+values[i]+"”).";continue;}
            // Windows reserves F12 for debuggers; preserve the historical global polling fallback.
            if(!binding.IsMouseButton&&i==2&&binding.Key==Key.F12) continue;
            if(binding.IsMouseButton)
            {
                if(mouseHook==IntPtr.Zero)
                {
                    if(firstError==null)firstError=labels[i]+": não foi possível registrar o monitoramento global dos botões do mouse (código "+Marshal.GetLastWin32Error()+").";
                    continue;
                }
                mouseActions[MouseActionKey(binding)]=names[i]; ids[names[i]]=0; continue;
            }
            int id=++nextId;
            if(RegisterHotKey(hwnd,id,(uint)binding.Modifiers|0x4000,(uint)binding.VirtualKey)){actions[id]=names[i];ids[names[i]]=id;}
            else if(firstError==null){int error=Marshal.GetLastWin32Error();firstError=labels[i]+": o Windows não conseguiu registrar "+values[i]+" (código "+error+"). Pode estar reservado ou em uso.";}
        }
        return firstError;
    }
    private static string MouseActionKey(HotkeyBinding binding) { return ((int)binding.Modifiers).ToString()+":"+binding.MouseButton; }
    internal void UnregisterAll()
    {
        foreach(int id in new List<int>(actions.Keys)) UnregisterHotKey(hwnd,id);
        actions.Clear(); ids.Clear(); mouseActions.Clear();
    }
    private IntPtr WndProc(IntPtr handle,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message==WmHotkey){string name;if(actions.TryGetValue(wParam.ToInt32(),out name)){handled=true;RaiseAction(name);}}
        return IntPtr.Zero;
    }
    private IntPtr MouseProc(int code,IntPtr wParam,IntPtr lParam)
    {
        if(code>=0)
        {
            string mouseButton=MouseButtonForMessage(wParam.ToInt32(),lParam);
            if(mouseButton!=null)
            {
                string action;
                var binding=new HotkeyBinding(HotkeyParser.CurrentModifiers(),mouseButton);
                if(mouseActions.TryGetValue(MouseActionKey(binding),out action))RaiseAction(action);
            }
        }
        return CallNextHookEx(mouseHook,code,wParam,lParam);
    }
    private static string MouseButtonForMessage(int message,IntPtr dataPointer)
    {
        switch(message)
        {
            case 0x0201:return "MouseLeft";
            case 0x0204:return "MouseRight";
            case 0x0207:return "MouseMiddle";
            case 0x020B:
                var data=(MouseHookData)Marshal.PtrToStructure(dataPointer,typeof(MouseHookData));
                int button=(int)((data.MouseData>>16)&0xFFFF);
                return button==1?"MouseX1":button==2?"MouseX2":null;
            default:return null;
        }
    }
    private void RaiseAction(string name) { var callback=ActionRequested;if(callback!=null)callback(name); }
    public void Dispose()
    {
        UnregisterAll(); if(source!=null)source.RemoveHook(WndProc);
        if(mouseHook!=IntPtr.Zero){UnhookWindowsHookEx(mouseHook);mouseHook=IntPtr.Zero;}
    }
    [StructLayout(LayoutKind.Sequential)] private struct MouseHookData { internal int X; internal int Y; internal uint MouseData; internal uint Flags; internal uint Time; internal IntPtr ExtraInfo; }
    private delegate IntPtr LowLevelMouseProc(int code,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool RegisterHotKey(IntPtr hWnd,int id,uint modifiers,uint virtualKey);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool UnregisterHotKey(IntPtr hWnd,int id);
    [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr SetWindowsHookEx(int idHook,LowLevelMouseProc callback,IntPtr module,uint threadId);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr wParam,IntPtr lParam);
    [DllImport("kernel32.dll",CharSet=CharSet.Auto,SetLastError=true)] private static extern IntPtr GetModuleHandle(string moduleName);
}
