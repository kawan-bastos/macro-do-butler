using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

internal sealed class UpdateBackupService
{
    private readonly string installDirectory;
    private readonly string userDataDirectory;
    internal UpdateBackupService(string installDirectory,string userDataDirectory){this.installDirectory=installDirectory;this.userDataDirectory=userDataDirectory;}
    internal string CreateBackup(string version)
    {
        string backupRoot=Path.Combine(userDataDirectory,"backups");Directory.CreateDirectory(backupRoot);
        string stamp=DateTime.Now.ToString("yyyy-MM-dd_HHmmss",CultureInfo.InvariantCulture);string destination=Path.Combine(backupRoot,"update_"+stamp+"_"+Guid.NewGuid().ToString("N").Substring(0,6));Directory.CreateDirectory(destination);
        var manifest=new UpdateBackupManifest{Application="Velqora",Version=version,CreatedAt=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture),Files=new List<UpdateBackupFile>()};
        try
        {
            CopyNamedFiles(installDirectory,destination,new[]{"presets.cfg","presets.cfg.bak","presets.cfg.migration.bak","presets.json","presets.json.bak","hotkeys.json","settings.json"},manifest,"install");
            string oldBackups=Path.Combine(installDirectory,"backups");if(Directory.Exists(oldBackups))CopyExistingBackups(oldBackups,destination,manifest);
            CopyNamedFiles(userDataDirectory,destination,new[]{"presets.cfg","presets.cfg.bak","presets.json","presets.json.bak","hotkeys.json","settings.json","macro-settings.json","update-preferences.json"},manifest,"user-data");
            using(var f=File.Create(Path.Combine(destination,"backup-manifest.json")))new DataContractJsonSerializer(typeof(UpdateBackupManifest)).WriteObject(f,manifest);
            return destination;
        }
        catch{try{Directory.Delete(destination,true);}catch{}throw;}
    }
    private static void CopyNamedFiles(string source,string destination,IEnumerable<string> names,UpdateBackupManifest manifest,string group)
    {if(!Directory.Exists(source))return;foreach(string name in names){string path=Path.Combine(source,name);if(File.Exists(path))CopyFile(path,Path.Combine(destination,group,Path.GetFileName(path)),manifest,group+"/"+Path.GetFileName(path));}}
    private static void CopyTree(string source,string destination,UpdateBackupManifest manifest,string relative)
    {
        Directory.CreateDirectory(destination);foreach(string file in Directory.GetFiles(source,"*",SearchOption.TopDirectoryOnly))CopyFile(file,Path.Combine(destination,Path.GetFileName(file)),manifest,relative+"/"+Path.GetFileName(file));
        foreach(string directory in Directory.GetDirectories(source,"*",SearchOption.TopDirectoryOnly)){string name=Path.GetFileName(directory);string target=Path.Combine(destination,name);CopyTree(directory,target,manifest,relative+"/"+name);}
    }
    private static void CopyExistingBackups(string source,string destination,UpdateBackupManifest manifest)
    {
        string targetRoot=Path.Combine(destination,"prior-backups");
        foreach(string file in Directory.GetFiles(source,"*",SearchOption.TopDirectoryOnly))CopyFile(file,Path.Combine(targetRoot,Path.GetFileName(file)),manifest,"prior-backups/"+Path.GetFileName(file));
        foreach(string directory in Directory.GetDirectories(source,"*",SearchOption.TopDirectoryOnly))
        {
            if(string.Equals(Path.GetFullPath(directory),Path.GetFullPath(destination),StringComparison.OrdinalIgnoreCase))continue;
            string name=Path.GetFileName(directory);CopyTree(directory,Path.Combine(targetRoot,name),manifest,"prior-backups/"+name);
        }
    }
    private static void CopyFile(string source,string destination,UpdateBackupManifest manifest,string relativeName=null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(source,destination,false);string hash;using(var sha=SHA256.Create())using(var f=File.OpenRead(destination))hash=BitConverter.ToString(sha.ComputeHash(f)).Replace("-","");
        manifest.Files.Add(new UpdateBackupFile{Name=relativeName??Path.GetFileName(destination),Source=source,Sha256=hash,Size=new FileInfo(destination).Length});
    }
}
