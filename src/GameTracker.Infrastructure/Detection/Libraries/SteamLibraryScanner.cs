using GameTracker.Core.Detection;
using GameTracker.Core.Domain;
using Microsoft.Win32;

namespace GameTracker.Infrastructure.Detection.Libraries;

/// <summary>Reads Steam libraries (libraryfolders.vdf) and their app manifests (appmanifest_*.acf).</summary>
internal sealed class SteamLibraryScanner : ILibraryScanner
{
    public string Name => "Steam";

    public IReadOnlyList<LibraryEntry> Scan()
    {
        var steamPath = FindSteamPath();
        if (steamPath is null)
            return [];

        var entries = new List<LibraryEntry>();
        foreach (var library in GetLibraryFolders(steamPath))
        {
            var steamApps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamApps))
                continue;

            foreach (var manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
            {
                var values = VdfReader.ReadFirstValues(File.ReadAllText(manifest));
                if (values.TryGetValue("appid", out var appId)
                    && values.TryGetValue("name", out var name)
                    && values.TryGetValue("installdir", out var installDir))
                {
                    entries.Add(new LibraryEntry(GameSource.Steam, appId, name, Path.Combine(steamApps, "common", installDir)));
                }
            }
        }
        return entries;
    }

    private static string? FindSteamPath() =>
        (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null)
         ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null)) is string path
        && Directory.Exists(path)
            ? Path.GetFullPath(path)
            : null;

    private static IEnumerable<string> GetLibraryFolders(string steamPath)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamPath };
        var file = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(file))
        {
            foreach (var path in VdfReader.ReadAllValues(File.ReadAllText(file), "path"))
                folders.Add(Path.GetFullPath(path));
        }
        return folders;
    }
}
