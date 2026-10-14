using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

[DataContract]
internal sealed class MacroSettings
{
    [DataMember(Order=1)] public int IntervalMilliseconds { get; set; }
}

internal sealed class MacroSettingsStorageService
{
    private const int DefaultInterval = 7;
    private readonly string filePath;

    internal MacroSettingsStorageService(string directory)
    {
        filePath=Path.Combine(directory,"macro-settings.json");
    }

    internal int LoadInterval()
    {
        if(!File.Exists(filePath))return DefaultInterval;
        try
        {
            using(var stream=File.OpenRead(filePath))
            {
                var settings=new DataContractJsonSerializer(typeof(MacroSettings)).ReadObject(stream) as MacroSettings;
                return settings==null||settings.IntervalMilliseconds<2||settings.IntervalMilliseconds>50?DefaultInterval:settings.IntervalMilliseconds;
            }
        }
        catch{return DefaultInterval;}
    }

    internal void SaveInterval(int interval)
    {
        if(interval<2||interval>50)throw new ArgumentOutOfRangeException("interval");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        string temporary=filePath+".tmp";
        try
        {
            using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            {
                new DataContractJsonSerializer(typeof(MacroSettings)).WriteObject(stream,new MacroSettings{IntervalMilliseconds=interval});
                stream.Flush(true);
            }
            if(File.Exists(filePath))File.Replace(temporary,filePath,null);
            else File.Move(temporary,filePath);
        }
        finally
        {
            if(File.Exists(temporary))File.Delete(temporary);
        }
    }
}
