using System.Runtime.Serialization;

[DataContract]
internal sealed class HotkeySettings
{
    [DataMember(Order=1)] public int FormatVersion { get; set; }
    [DataMember(Order=2)] public string ToggleMacro { get; set; }
    [DataMember(Order=3)] public string SwitchWeapon { get; set; }
    [DataMember(Order=4)] public string ExitApplication { get; set; }
    [DataMember(Order=5, EmitDefaultValue=false)] public string ShowWindow { get; set; }

    internal static HotkeySettings Defaults() { return new HotkeySettings { FormatVersion=1, ToggleMacro="F1", SwitchWeapon="F2", ExitApplication="F12" }; }
    internal HotkeySettings Copy() { return new HotkeySettings { FormatVersion=1, ToggleMacro=ToggleMacro, SwitchWeapon=SwitchWeapon, ExitApplication=ExitApplication, ShowWindow=ShowWindow }; }
}
