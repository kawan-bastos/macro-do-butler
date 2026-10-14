using System;
using System.IO;
using System.Runtime.Serialization.Json;

internal sealed class HotkeyStorageService
{
    internal string FilePath { get; private set; }
    private readonly string fallbackPath;
    internal HotkeyStorageService(string directory) { FilePath=Path.Combine(directory,"hotkeys.json"); fallbackPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"hotkeys.json"); }
    internal HotkeySettings Load(out string warning)
    {
        warning=null;
        if(File.Exists(FilePath))
        {
            try { return Read(FilePath); }
            catch { warning="Não foi possível ler hotkeys.json. Os atalhos padrão foram carregados; o arquivo existente foi preservado."; return HotkeySettings.Defaults(); }
        }
        if(!string.Equals(FilePath,fallbackPath,StringComparison.OrdinalIgnoreCase)&&File.Exists(fallbackPath))
        {
            try { FilePath=fallbackPath; return Read(FilePath); }
            catch { warning="Não foi possível ler hotkeys.json. Os atalhos padrão foram carregados; o arquivo existente foi preservado."; return HotkeySettings.Defaults(); }
        }
        var defaults=HotkeySettings.Defaults();
        try { SaveAt(FilePath,defaults); return defaults; }
        catch(UnauthorizedAccessException) { FilePath=fallbackPath; SaveAt(FilePath,defaults); return defaults; }
        catch(IOException) { FilePath=fallbackPath; SaveAt(FilePath,defaults); return defaults; }
    }
    internal void Save(HotkeySettings settings)
    {
        try { SaveAt(FilePath,settings); }
        catch(UnauthorizedAccessException) { if(string.Equals(FilePath,fallbackPath,StringComparison.OrdinalIgnoreCase))throw; FilePath=fallbackPath; SaveAt(FilePath,settings); }
        catch(IOException) { if(string.Equals(FilePath,fallbackPath,StringComparison.OrdinalIgnoreCase))throw; FilePath=fallbackPath; SaveAt(FilePath,settings); }
    }
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
