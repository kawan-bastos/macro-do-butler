using System;
using System.IO;
using System.Runtime.Serialization.Json;

internal sealed class HotkeyStorageService
{
    internal string FilePath { get; private set; }

    internal HotkeyStorageService(string directory) { FilePath=Path.Combine(directory,"hotkeys.json"); }
    internal HotkeySettings Load(out string warning)
    {
        warning=null;
        if(File.Exists(FilePath))
        {
            try { return Read(FilePath); }
            catch { warning="Não foi possível ler hotkeys.json. Os atalhos padrão foram carregados; o arquivo existente foi preservado."; return HotkeySettings.Defaults(); }
        }
        var defaults=HotkeySettings.Defaults();
        SaveAt(FilePath,defaults);
        return defaults;
    }
    internal void Save(HotkeySettings settings) { SaveAt(FilePath,settings); }
    private static HotkeySettings Read(string path)
    {
        using(var stream=File.OpenRead(path)){var s=new DataContractJsonSerializer(typeof(HotkeySettings)).ReadObject(stream) as HotkeySettings; if(s==null||s.FormatVersion!=1||HotkeyValidator.Validate(s)!=null)throw new InvalidDataException(); return s;}
    }
    private static void SaveAt(string path,HotkeySettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp=path+".tmp";
        using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){new DataContractJsonSerializer(typeof(HotkeySettings)).WriteObject(stream,settings); stream.Flush(true);}
        if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
    }
}
