using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

// Migration is completed before any service is allowed to load or save user data.
internal static class UserDataService
{
    internal static string DirectoryPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Velqora"); } }
    internal static readonly string[] Names = { "presets.cfg", "presets.cfg.bak", "presets.cfg.migration.bak", "presets.json", "presets.json.bak", "hotkeys.json", "settings.json", "macro-settings.json", "update-preferences.json" };

    internal static string Initialize()
    {
        string app = AppDomain.CurrentDomain.BaseDirectory;
        string legacy = Path.Combine(app, "legacy-data-source.txt");
        string selected = File.Exists(legacy) ? File.ReadAllText(legacy).Trim() : app;
        if (!File.Exists(Path.Combine(DirectoryPath, ".migration-complete")) && File.Exists(legacy) && !Directory.Exists(selected))
            throw new IOException("A pasta antiga informada não está disponível. Reconecte-a antes de abrir o Velqora: " + selected);
        return Migrate(DirectoryPath, new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacroDoButler"), selected, app });
    }

    internal static string Migrate(string destination, string[] sources)
    {
        destination = Path.GetFullPath(destination);
        EnsureSafe(destination);
        Directory.CreateDirectory(destination);
        string marker = Path.Combine(destination, ".migration-complete");
        if (File.Exists(marker)) return destination;
        // Each attempt is retained, including failed attempts. Existing destination files win.
        string backup = Path.Combine(destination, "migration-backups", Guid.NewGuid().ToString("N"));
        int index = 0;
        foreach (string source in sources.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(source) || string.Equals(Path.GetFullPath(source).TrimEnd('\\'), destination.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) continue;
            EnsureSafe(source);
            string snapshot = Path.Combine(backup, (index++).ToString());
            foreach (string name in Names)
                if (File.Exists(Path.Combine(source, name))) CopyVerified(Path.Combine(source, name), Path.Combine(snapshot, name));
            string oldBackups = Path.Combine(source, "backups");
            if (Directory.Exists(oldBackups)) CopyTree(oldBackups, Path.Combine(snapshot, "backups"), false);
            if (Directory.Exists(snapshot)) CopyTree(snapshot, destination, true);
        }
        File.WriteAllText(marker, "Velqora migration completed UTC " + DateTime.UtcNow.ToString("o"));
        return destination;
    }

    private static void CopyTree(string source, string target, bool preserve)
    {
        EnsureSafe(source); EnsureSafe(target);
        foreach (string file in Directory.GetFiles(source))
        {
            string output = Path.Combine(target, Path.GetFileName(file));
            if (!preserve || !File.Exists(output)) CopyVerified(file, output);
        }
        foreach (string directory in Directory.GetDirectories(source)) CopyTree(directory, Path.Combine(target, Path.GetFileName(directory)), preserve);
    }

    private static void CopyVerified(string source, string target)
    {
        EnsureSafe(source); EnsureSafe(target);
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        string temporary = Path.Combine(Path.GetDirectoryName(target), Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp");
        File.Copy(source, temporary, false);
        using (var sha = SHA256.Create())
        using (var input = File.OpenRead(source))
        using (var output = File.OpenRead(temporary))
            if (!sha.ComputeHash(input).SequenceEqual(sha.ComputeHash(output))) throw new IOException("Falha ao validar migração: " + source);
        File.Move(temporary, target);
    }

    internal static void EnsureSafe(string path)
    {
        for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("A migração não aceita links ou junções: " + current);
    }
}
