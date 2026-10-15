using System;
using System.IO;
using System.Runtime.Serialization.Json;

internal sealed class UpdatePreferencesStorageService
{
    private readonly string path;
    internal UpdatePreferencesStorageService(string directory){path=Path.Combine(directory,"update-preferences.json");}
    internal UpdatePreferences Load(){try{if(File.Exists(path))using(var f=File.OpenRead(path)){var p=new DataContractJsonSerializer(typeof(UpdatePreferences)).ReadObject(f) as UpdatePreferences;if(p!=null){if(p.PreviousVersionsToKeep<1)p.PreviousVersionsToKeep=1;return p;}}}catch{}return UpdatePreferences.Defaults();}
    internal void Save(UpdatePreferences preferences){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";using(var f=File.Create(temp))new DataContractJsonSerializer(typeof(UpdatePreferences)).WriteObject(f,preferences);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
}
