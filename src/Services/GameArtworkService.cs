using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Read-only, local artwork lookup. No downloads, account access, or changes to game files.
internal sealed class GameArtworkService
{
    internal static string Key(string name)
    {
        return new string((name ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    internal IDictionary<string, ImageSource> LoadLocalArtwork()
    {
        var result = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var roots = SteamGameDiscoveryService.FindSteamRoots().ToList();
            foreach (var game in new SteamGameDiscoveryService().FindInstalledGames().Games)
            {
                ImageSource image = null;
                foreach (string root in roots)
                {
                    string cache = Path.Combine(root, "appcache", "librarycache");
                    image = ReadBitmap(Path.Combine(cache, game.AppId + "_library_600x900.jpg"))
                        ?? ReadBitmap(Path.Combine(cache, game.AppId + "_icon.jpg"))
                        ?? ReadBitmap(Path.Combine(cache, game.AppId + "_header.jpg"));
                    if (image == null)
                    {
                        string appCache = Path.Combine(cache, game.AppId.ToString());
                        image = ReadFirstCachedImage(appCache);
                    }
                    if (image != null) break;
                }
                image = image ?? ReadExecutableIcon(game.InstallDirectory);
                if (image != null) result[Key(game.Name)] = image;
            }
        }
        catch { }
        foreach (string platform in new[] { "epic", "ubisoft" })
        {
            try
            {
                foreach (var game in new ExternalLauncherGameDiscoveryService().FindGames(platform).Games)
                {
                    string key = Key(game.Name);
                    if (result.ContainsKey(key)) continue;
                    var image = ReadExecutableIcon(game.InstallDirectory);
                    if (image != null) result[key] = image;
                }
            }
            catch { }
        }
        return result;
    }

    private static ImageSource ReadFirstCachedImage(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return null;
            var candidates = Directory.EnumerateFiles(directory).Where(p =>
                new[] { ".jpg", ".png", ".jpeg" }.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .OrderByDescending(p => Path.GetFileName(p).IndexOf("600x900", StringComparison.OrdinalIgnoreCase) >= 0).Take(12);
            foreach (var file in candidates) { var image = ReadBitmap(file); if (image != null) return image; }
        }
        catch { }
        return null;
    }

    private static ImageSource ReadBitmap(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using (var stream = File.OpenRead(path))
            {
                var image = new BitmapImage();
                image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 80; image.StreamSource = stream; image.EndInit();
                image.Freeze(); return image;
            }
        }
        catch { return null; }
    }

    private static ImageSource ReadExecutableIcon(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return null;
            foreach (string file in Directory.EnumerateFiles(directory, "*.exe").Where(p =>
                p.IndexOf("unins", StringComparison.OrdinalIgnoreCase) < 0 &&
                p.IndexOf("setup", StringComparison.OrdinalIgnoreCase) < 0).Take(12))
            {
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(file))
                {
                    if (icon == null) continue;
                    var image = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(48, 48));
                    image.Freeze(); return image;
                }
            }
        }
        catch { }
        return null;
    }
}
