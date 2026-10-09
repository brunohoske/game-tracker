using GameTracker.Core.Detection;
using GameTracker.Core.Domain;
using Microsoft.Win32;

namespace GameTracker.Infrastructure.Detection.Libraries;

/// <summary>GOG Galaxy writes one registry key per installed game.</summary>
internal sealed class GogLibraryScanner : ILibraryScanner
{
    public string Name => "GOG";

    public IReadOnlyList<LibraryEntry> Scan() =>
        RegistryScan.SubKeys(@"SOFTWARE\WOW6432Node\GOG.com\Games", (id, key) =>
            key.GetValue("path") is string path && key.GetValue("gameName") is string name
                ? new LibraryEntry(GameSource.Gog, key.GetValue("gameID") as string ?? id, name, path)
                : null);
}

/// <summary>Ubisoft Connect registers install folders (the folder name is the game title).</summary>
internal sealed class UbisoftLibraryScanner : ILibraryScanner
{
    public string Name => "Ubisoft Connect";

    public IReadOnlyList<LibraryEntry> Scan() =>
        RegistryScan.SubKeys(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs", (id, key) =>
            key.GetValue("InstallDir") is string { Length: > 0 } path
                ? new LibraryEntry(GameSource.Ubisoft, id, Path.GetFileName(path.TrimEnd('/', '\\')), path.Replace('/', '\\'))
                : null);
}

internal static class RegistryScan
{
    public static IReadOnlyList<LibraryEntry> SubKeys(string path, Func<string, RegistryKey, LibraryEntry?> read)
    {
        using var root = Registry.LocalMachine.OpenSubKey(path);
        if (root is null)
            return [];

        var entries = new List<LibraryEntry>();
        foreach (var name in root.GetSubKeyNames())
        {
            using var key = root.OpenSubKey(name);
            if (key is not null && read(name, key) is { } entry && Directory.Exists(entry.InstallDirectory))
                entries.Add(entry);
        }
        return entries;
    }
}
