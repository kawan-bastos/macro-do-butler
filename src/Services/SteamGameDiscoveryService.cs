using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

internal sealed class SteamInstalledGame
{
    internal uint AppId { get; set; }
    internal string Name { get; set; }
    internal string InstallDirectory { get; set; }
}

internal sealed class SteamGameDiscoveryResult
{
    internal IList<SteamInstalledGame> Games { get; set; }
    internal bool SomeAppsCouldNotBeClassified { get; set; }
}

internal sealed class SteamGameDiscoveryService
{
    private static readonly Regex NamePattern = new Regex("\\\"name\\\"\\s+\\\"((?:\\\\.|[^\\\"])+)\\\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex InstallDirectoryPattern = new Regex("\\\"installdir\\\"\\s+\\\"((?:\\\\.|[^\\\"])+)\\\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LibraryPathPattern = new Regex("\\\"path\\\"\\s+\\\"((?:\\\\.|[^\\\"])+)\\\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AppIdPattern = new Regex("appmanifest_(\\d+)\\.acf$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal SteamGameDiscoveryResult FindInstalledGames()
    {
        var found = new Dictionary<string, SteamInstalledGame>(StringComparer.OrdinalIgnoreCase);
        IList<string> steamRoots=FindSteamRoots().ToList();
        IDictionary<uint,string> appTypes=FindLocalAppTypes(steamRoots);
        foreach (string root in steamRoots)
        {
            foreach (string steamApps in FindSteamAppsDirectories(root))
            {
                if (!Directory.Exists(steamApps)) continue;
                string common = Path.Combine(steamApps, "common");
                string[] manifests;
                try { manifests = Directory.GetFiles(steamApps, "appmanifest_*.acf", SearchOption.TopDirectoryOnly); }
                catch { continue; }

                foreach (string manifest in manifests)
                {
                    try
                    {
                        string text = File.ReadAllText(manifest);
                        Match nameMatch = NamePattern.Match(text);
                        Match installMatch = InstallDirectoryPattern.Match(text);
                        Match appIdMatch = AppIdPattern.Match(Path.GetFileName(manifest));
                        uint appId;
                        if (!nameMatch.Success || !installMatch.Success || !appIdMatch.Success || !uint.TryParse(appIdMatch.Groups[1].Value, out appId)) continue;
                        string name = Unescape(nameMatch.Groups[1].Value).Trim();
                        string installDirectory = Unescape(installMatch.Groups[1].Value).Trim();
                        if (name.Length == 0 || installDirectory.Length == 0) continue;
                        string path = Path.Combine(common, installDirectory);
                        if (!Directory.Exists(path) || found.ContainsKey(name)) continue;
                        found.Add(name, new SteamInstalledGame { AppId = appId, Name = name, InstallDirectory = path });
                    }
                    catch { /* Ignore incomplete or inaccessible manifests and continue scanning. */ }
                }
            }
        }
        var confirmedGames = new System.Collections.Concurrent.ConcurrentBag<SteamInstalledGame>();
        int failedClassifications = 0;
        Parallel.ForEach(found.Values, new ParallelOptions { MaxDegreeOfParallelism = 3 }, game =>
        {
            try
            {
                string localType;
                if (appTypes.TryGetValue(game.AppId, out localType))
                {
                    // appinfo.vdf's common/type is Steam's product classification.
                    // Keep only actual games; software, tools, demos, etc. are excluded
                    // without maintaining a list of specific AppIDs.
                    if (localType.Equals("game", StringComparison.OrdinalIgnoreCase)) confirmedGames.Add(game);
                }
                else Interlocked.Increment(ref failedClassifications);
            }
            catch { Interlocked.Increment(ref failedClassifications); }
        });

        return new SteamGameDiscoveryResult
        {
            Games = confirmedGames.OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            SomeAppsCouldNotBeClassified = failedClassifications > 0
        };
    }

    private static IDictionary<uint,string> FindLocalAppTypes(IEnumerable<string> steamRoots)
    {
        var result=new Dictionary<uint,string>();
        foreach(string root in steamRoots)
        {
            string appInfo=Path.Combine(root,"appcache","appinfo.vdf");
            if(!File.Exists(appInfo))continue;
            try{ReadAppInfoTypes(appInfo,result);}catch{ /* Use the online metadata fallback for entries not parsed locally. */ }
        }
        return result;
    }

    // appinfo.vdf v41 stores the authoritative Steam product type (Game, Application,
    // Tool, etc.) in the binary KeyValues common/type field.
    private static void ReadAppInfoTypes(string path,IDictionary<uint,string> result)
    {
        byte[] data=File.ReadAllBytes(path);
        if(data.Length<16)return;
        uint magic=BitConverter.ToUInt32(data,0);
        if((magic&0xff)!=41)return;
        long tableOffset=BitConverter.ToInt64(data,8);
        if(tableOffset<16||tableOffset>data.Length-4)return;
        int tableCursor=(int)tableOffset;
        uint count=BitConverter.ToUInt32(data,tableCursor);tableCursor+=4;
        if(count>1000000)return;
        var strings=new List<string>((int)count);
        for(uint i=0;i<count;i++)strings.Add(ReadCString(data,ref tableCursor,data.Length));

        int cursor=16;
        while(cursor+8<=data.Length)
        {
            uint appId=BitConverter.ToUInt32(data,cursor);if(appId==0)break;
            uint size=BitConverter.ToUInt32(data,cursor+4);
            long entryEndLong=(long)cursor+8+size;if(entryEndLong>data.Length||entryEndLong<cursor)break;
            int entryEnd=(int)entryEndLong;
            // v41 fixed entry data: state, timestamp, access token, SHA-1, change number, SHA-1.
            int blobStart=cursor+8+60;
            if(blobStart<entryEnd)
            {
                int blobCursor=blobStart;string appType=null;
                ReadBinaryObject(data, ref blobCursor, entryEnd, strings, false, ref appType, 0);
                if(!string.IsNullOrWhiteSpace(appType))result[appId]=appType.Trim();
            }
            cursor=entryEnd;
        }
    }

    private static void ReadBinaryObject(byte[] data,ref int cursor,int end,IList<string> strings,bool insideCommon,ref string appType,int depth)
    {
        if(depth>64)return;
        while(cursor<end)
        {
            byte valueType=data[cursor++];
            if(valueType==8)return;
            if(cursor+4>end)return;
            int keyIndex=BitConverter.ToInt32(data,cursor);cursor+=4;
            if(keyIndex<0||keyIndex>=strings.Count)return;
            string key=strings[keyIndex];
            switch(valueType)
            {
                case 0:
                    // Only inspect a direct common/type field. The old flag propagated
                    // into every nested section and could overwrite it with unrelated
                    // type fields later in the appinfo object.
                    ReadBinaryObject(data, ref cursor, end, strings,
                        key.Equals("common", StringComparison.OrdinalIgnoreCase), ref appType, depth + 1);
                    break;
                case 1:
                    string value=ReadCString(data,ref cursor,end);
                    if(insideCommon&&key.Equals("type",StringComparison.OrdinalIgnoreCase))appType=value;
                    break;
                case 2: case 3: case 4: case 6:
                    if(cursor+4>end)return;cursor+=4;break;
                case 5:
                    while(cursor+1<end){if(data[cursor]==0&&data[cursor+1]==0){cursor+=2;break;}cursor+=2;}break;
                case 7: case 10:
                    if(cursor+8>end)return;cursor+=8;break;
                default:
                    return;
            }
        }
    }

    private static string ReadCString(byte[] data,ref int cursor,int end)
    {
        int start=cursor;while(cursor<end&&data[cursor]!=0)cursor++;
        string value=System.Text.Encoding.UTF8.GetString(data,start,cursor-start);
        if(cursor<end)cursor++;
        return value;
    }

    internal static IEnumerable<string> FindSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                    using (RegistryKey steamKey = baseKey.OpenSubKey("SOFTWARE\\Valve\\Steam"))
                    {
                        if (steamKey == null) continue;
                        AddRoot(roots, steamKey.GetValue("SteamPath") as string);
                        AddRoot(roots, steamKey.GetValue("InstallPath") as string);
                    }
                }
                catch { }
            }
        }

        AddRoot(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        AddRoot(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));
        return roots;
    }

    private static void AddRoot(ISet<string> roots, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        string normalized = path.Trim().Replace('/', '\\');
        if (Directory.Exists(normalized)) roots.Add(normalized);
    }

    private static IEnumerable<string> FindSteamAppsDirectories(string root)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string rootApps = Path.Combine(root, "steamapps");
        if (Directory.Exists(rootApps)) directories.Add(rootApps);
        string libraryFile = Path.Combine(rootApps, "libraryfolders.vdf");
        if (!File.Exists(libraryFile)) return directories;

        try
        {
            string content = File.ReadAllText(libraryFile);
            foreach (Match match in LibraryPathPattern.Matches(content))
            {
                string library = Unescape(match.Groups[1].Value).Trim();
                if (library.Length == 0) continue;
                string apps = Path.Combine(library, "steamapps");
                if (Directory.Exists(apps)) directories.Add(apps);
            }
        }
        catch { }
        return directories;
    }

    private static string Unescape(string value)
    {
        return value.Replace("\\\\", "\\").Replace("\\\"", "\"");
    }
}
