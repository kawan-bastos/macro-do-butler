using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

internal sealed class DetectedLauncherGame
{
    internal string Name { get; set; }
    internal string InstallDirectory { get; set; }
    internal string Launcher { get; set; }
}

internal sealed class LauncherGameDiscoveryResult
{
    internal IList<DetectedLauncherGame> Games { get; set; }
    internal bool LauncherDataFound { get; set; }
    internal int EntriesSkipped { get; set; }
}

internal sealed class ExternalLauncherGameDiscoveryService
{
    internal LauncherGameDiscoveryResult FindGames(string launcher)
    {
        switch ((launcher ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "rockstar": return FindRockstarGames();
            case "ubisoft": return FindUbisoftGames();
            case "epic games":
            case "epic": return FindEpicGames();
            default: throw new ArgumentOutOfRangeException("launcher", "Launcher não suportado.");
        }
    }

    private static LauncherGameDiscoveryResult FindEpicGames()
    {
        string manifestDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");
        var games = new Dictionary<string, DetectedLauncherGame>(StringComparer.OrdinalIgnoreCase);
        int skipped = 0;
        bool found = Directory.Exists(manifestDirectory);

        if (found)
        {
            string[] manifests;
            try { manifests = Directory.GetFiles(manifestDirectory, "*.item", SearchOption.TopDirectoryOnly); }
            catch { manifests = new string[0]; }

            foreach (string manifestPath in manifests)
            {
                try
                {
                    EpicManifest manifest;
                    var serializer = new DataContractJsonSerializer(typeof(EpicManifest));
                    using (var stream = File.OpenRead(manifestPath))
                        manifest = serializer.ReadObject(stream) as EpicManifest;

                    if (manifest == null || manifest.IsApplication || manifest.IsIncompleteInstall) continue;
                    string name = (manifest.DisplayName ?? string.Empty).Trim();
                    string installDirectory = NormalizeDirectory(manifest.InstallLocation);
                    string executable = manifest.LaunchExecutable ?? string.Empty;
                    if (name.Length == 0 || installDirectory.Length == 0 || executable.Trim().Length == 0 || !Directory.Exists(installDirectory)) continue;
                    string executablePath = Path.IsPathRooted(executable)
                        ? executable
                        : Path.Combine(installDirectory, executable.TrimStart('\\', '/'));
                    if (!File.Exists(executablePath)) continue;

                    AddGame(games, name, installDirectory, "Epic Games");
                }
                catch { skipped++; }
            }
        }

        return Result(games, found, skipped);
    }

    private static LauncherGameDiscoveryResult FindUbisoftGames()
    {
        var games = new Dictionary<string, DetectedLauncherGame>(StringComparer.OrdinalIgnoreCase);
        int skipped = 0;
        bool found = false;
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (RegistryKey installs = baseKey.OpenSubKey("SOFTWARE\\Ubisoft\\Launcher\\Installs"))
                {
                    if (installs == null) continue;
                    found = true;
                    foreach (string gameId in installs.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey gameKey = installs.OpenSubKey(gameId))
                            {
                                if (gameKey == null) continue;
                                string installDirectory = NormalizeDirectory(gameKey.GetValue("InstallDir") as string);
                                if (installDirectory.Length == 0 || !Directory.Exists(installDirectory) || !HasGameExecutable(installDirectory)) continue;
                                string name = FirstValue(gameKey, "DisplayName", "GameName", "Name", "Title");
                                if (name.Length == 0) name = Path.GetFileName(installDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                                AddGame(games, name, installDirectory, "Ubisoft");
                            }
                        }
                        catch { skipped++; }
                    }
                }
            }
            catch { /* Registry view may be inaccessible; inspect the other view. */ }
        }
        return Result(games, found, skipped);
    }

    private static LauncherGameDiscoveryResult FindRockstarGames()
    {
        var games = new Dictionary<string, DetectedLauncherGame>(StringComparer.OrdinalIgnoreCase);
        int skipped = 0;
        bool found = false;
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (RegistryKey rockstar = baseKey.OpenSubKey("SOFTWARE\\Rockstar Games"))
                {
                    if (rockstar == null) continue;
                    found = true;
                    ScanRockstarRegistryKey(rockstar, 0, games, ref skipped);
                }
            }
            catch { /* Registry view may be inaccessible; inspect the other view. */ }
        }
        return Result(games, found, skipped);
    }

    private static void ScanRockstarRegistryKey(RegistryKey key, int depth,
        IDictionary<string, DetectedLauncherGame> games, ref int skipped)
    {
        string keyName = key.Name.Split('\\').LastOrDefault() ?? string.Empty;
        if (IsLauncherComponent(keyName)) return;

        try
        {
            string installDirectory = NormalizeDirectory(FirstValue(key, "InstallFolder", "InstallDir", "InstallLocation", "InstallPath"));
            if (installDirectory.Length > 0 && Directory.Exists(installDirectory) && HasGameExecutable(installDirectory))
            {
                string name = FirstValue(key, "DisplayName", "GameName", "Title", "Name");
                if (name.Length == 0) name = keyName;
                AddGame(games, name, installDirectory, "Rockstar");
            }
        }
        catch { skipped++; }

        if (depth >= 3) return;
        string[] children;
        try { children = key.GetSubKeyNames(); }
        catch { skipped++; return; }
        foreach (string childName in children)
        {
            if (IsLauncherComponent(childName)) continue;
            try
            {
                using (RegistryKey child = key.OpenSubKey(childName))
                    if (child != null) ScanRockstarRegistryKey(child, depth + 1, games, ref skipped);
            }
            catch { skipped++; }
        }
    }

    private static bool HasGameExecutable(string directory)
    {
        try
        {
            return Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly)
                .Any(path => !IsLauncherComponent(Path.GetFileNameWithoutExtension(path)));
        }
        catch { return false; }
    }

    private static bool IsLauncherComponent(string value)
    {
        string normalized = (value ?? string.Empty).Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
        return normalized.IndexOf("launcher", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("socialclub", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("redistributable", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("rockstarservice", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string FirstValue(RegistryKey key, params string[] names)
    {
        foreach (string name in names)
        {
            string value = key.GetValue(name) as string;
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return string.Empty;
    }

    private static string NormalizeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try { return Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')); }
        catch { return string.Empty; }
    }

    private static void AddGame(IDictionary<string, DetectedLauncherGame> games, string name, string path, string launcher)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length == 0 || string.IsNullOrWhiteSpace(path)) return;
        string key = name + "|" + Path.GetFullPath(path);
        if (!games.ContainsKey(key)) games.Add(key, new DetectedLauncherGame { Name = name, InstallDirectory = path, Launcher = launcher });
    }

    private static LauncherGameDiscoveryResult Result(IDictionary<string, DetectedLauncherGame> games, bool launcherFound, int skipped)
    {
        return new LauncherGameDiscoveryResult
        {
            Games = games.Values.OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            LauncherDataFound = launcherFound,
            EntriesSkipped = skipped
        };
    }

    [DataContract]
    private sealed class EpicManifest
    {
        [DataMember(Name = "DisplayName")] public string DisplayName { get; set; }
        [DataMember(Name = "InstallLocation")] public string InstallLocation { get; set; }
        [DataMember(Name = "LaunchExecutable")] public string LaunchExecutable { get; set; }
        [DataMember(Name = "bIsApplication")] public bool IsApplication { get; set; }
        [DataMember(Name = "bIsIncompleteInstall")] public bool IsIncompleteInstall { get; set; }
    }
}
