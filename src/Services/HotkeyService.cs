using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;

internal sealed class HotkeyService : IDisposable
{
    private const int WhMouseLl=14;
    private readonly Dictionary<string,string> mouseActions=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    private readonly GlobalKeyboardShortcutPoller keyboardPoller=new GlobalKeyboardShortcutPoller(new Win32KeyboardStateReader());
    private readonly LowLevelMouseProc mouseProc;
    private readonly Thread keyboardThread;
    private IntPtr mouseHook;
    private volatile bool disposed;
    internal event Action<string> ActionRequested;
    internal string LastRegistrationWarning { get; private set; }

    internal HotkeyService(IntPtr handle)
    {
        mouseProc=MouseProc;
        keyboardThread=new Thread(PollKeyboard){IsBackground=true,Name="Atalhos globais"};
        keyboardThread.Start();
    }
    internal string Register(HotkeySettings settings)
    {
        string removal=UnregisterAll();
        if(removal!=null)return LastRegistrationWarning=removal;
        return LastRegistrationWarning=RegisterAvailable(settings);
    }
    internal string TryReplace(HotkeySettings previous,HotkeySettings candidate)
    {
        string removal=UnregisterAll();
        if(removal!=null)return LastRegistrationWarning=removal;
        LastRegistrationWarning=RegisterAvailable(candidate);
        string[] names={"ToggleMacro","SwitchWeapon","ExitApplication","ShowWindow"};
        string[] oldValues={previous.ToggleMacro,previous.SwitchWeapon,previous.ExitApplication,previous.ShowWindow};
        string[] newValues={candidate.ToggleMacro,candidate.SwitchWeapon,candidate.ExitApplication,candidate.ShowWindow};
        bool changed=false;string failedAction=null;
        for(int i=0;i<names.Length;i++)
        {
            if(string.Equals(oldValues[i],newValues[i],StringComparison.Ordinal))continue;
            changed=true;
            if(!string.IsNullOrWhiteSpace(newValues[i])&&!IsActionActive(names[i],candidate))failedAction=names[i];
        }
        if(changed&&failedAction!=null)
        {
            string failure=LastRegistrationWarning??"O novo atalho não pôde ser ativado.";
            string rollbackRemoval=UnregisterAll();
            if(rollbackRemoval==null)Register(previous);
            return LastRegistrationWarning=rollbackRemoval==null?failure:failure+" "+rollbackRemoval;
        }
        return null;
    }
    private string RegisterAvailable(HotkeySettings settings)
    {
        string firstError=null;
        string[] names={"ToggleMacro","SwitchWeapon","ExitApplication","ShowWindow"};
        string[] labels={"Ligar/desligar macro","Alternar arma","Fechar aplicativo","Mostrar janela"};
        string[] values={settings.ToggleMacro,settings.SwitchWeapon,settings.ExitApplication,settings.ShowWindow};
        var keyboard=new List<GlobalKeyboardShortcut>();
        for(int i=0;i<values.Length;i++)
        {
            if(string.IsNullOrWhiteSpace(values[i]))continue;
            HotkeyBinding binding;
            if(!HotkeyParser.TryParse(values[i],out binding)){if(firstError==null)firstError=labels[i]+": combinação inválida (“"+values[i]+"”).";continue;}
            if(binding.IsMouseButton)
            {
                if(!EnsureMouseHook(out int error)){if(firstError==null)firstError=HookError(labels[i],error);continue;}
                mouseActions[MouseActionKey(binding)]=names[i];
            }
            else keyboard.Add(new GlobalKeyboardShortcut(names[i],binding.VirtualKey,(int)binding.Modifiers));
        }
        keyboardPoller.SetShortcuts(keyboard);
        return firstError;
    }
    private bool IsActionActive(string action,HotkeySettings settings)
    {
        string value=action=="ToggleMacro"?settings.ToggleMacro:action=="SwitchWeapon"?settings.SwitchWeapon:action=="ExitApplication"?settings.ExitApplication:settings.ShowWindow;
        HotkeyBinding binding;
        if(!HotkeyParser.TryParse(value,out binding))return false;
        return !binding.IsMouseButton||mouseActions.ContainsValue(action);
    }
    private bool EnsureMouseHook(out int error)
    {
        error=0;if(mouseHook!=IntPtr.Zero)return true;
        IntPtr module=GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName);
        mouseHook=SetWindowsHookEx(WhMouseLl,mouseProc,module,0);
        if(mouseHook!=IntPtr.Zero)return true;
        error=Marshal.GetLastWin32Error();return false;
    }
    private static string HookError(string label,int error){return label+": não foi possível ativar o atalho global do mouse (código do Windows "+error+").";}
    private static string MouseActionKey(HotkeyBinding binding){return ((int)binding.Modifiers).ToString()+":"+binding.MouseButton;}
    internal string UnregisterAll()
    {
        if(mouseHook!=IntPtr.Zero)
        {
            if(!UnhookWindowsHookEx(mouseHook))return "O Windows não permitiu remover o monitoramento global do mouse (código "+Marshal.GetLastWin32Error()+").";
            mouseHook=IntPtr.Zero;
        }
        keyboardPoller.SetShortcuts(new GlobalKeyboardShortcut[0]);
        mouseActions.Clear();
        return null;
    }
    private void PollKeyboard()
    {
        while(!disposed)
        {
            foreach(string action in keyboardPoller.PollOnce())RaiseAction(action);
            Thread.Sleep(8);
        }
    }
    private IntPtr MouseProc(int code,IntPtr wParam,IntPtr lParam)
    {
        if(!disposed&&code>=0)
        {
            string button=MouseButtonForMessage(wParam.ToInt32(),lParam);
            if(button!=null){string action;var binding=new HotkeyBinding(HotkeyParser.CurrentModifiers(),button);if(mouseActions.TryGetValue(MouseActionKey(binding),out action))RaiseAction(action);}
        }
        return CallNextHookEx(mouseHook,code,wParam,lParam);
    }
    private static string MouseButtonForMessage(int message,IntPtr pointer)
    {
        switch(message){case 0x0201:return "MouseLeft";case 0x0204:return "MouseRight";case 0x0207:return "MouseMiddle";case 0x020B:var data=(MouseHookData)Marshal.PtrToStructure(pointer,typeof(MouseHookData));int button=(int)((data.MouseData>>16)&0xFFFF);return button==1?"MouseX1":button==2?"MouseX2":null;default:return null;}
    }
    private void RaiseAction(string name){var callback=ActionRequested;if(callback!=null)callback(name);}
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        keyboardPoller.Dispose();
        if(keyboardThread!=null&&keyboardThread.IsAlive)keyboardThread.Join(500);
        UnregisterAll();
        if(mouseHook!=IntPtr.Zero&&UnhookWindowsHookEx(mouseHook))mouseHook=IntPtr.Zero;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MouseHookData{internal int X;internal int Y;internal uint MouseData;internal uint Flags;internal uint Time;internal IntPtr ExtraInfo;}
    private delegate IntPtr LowLevelMouseProc(int code,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr SetWindowsHookEx(int idHook,LowLevelMouseProc callback,IntPtr module,uint threadId);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr wParam,IntPtr lParam);
    [DllImport("kernel32.dll",CharSet=CharSet.Auto,SetLastError=true)] private static extern IntPtr GetModuleHandle(string moduleName);
}
