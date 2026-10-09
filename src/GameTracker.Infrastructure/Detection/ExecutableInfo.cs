using System.Diagnostics;
using System.Text.RegularExpressions;
using GameTracker.Core.Common;

namespace GameTracker.Infrastructure.Detection;

/// <summary>Guesses a readable game title for an executable found outside any launcher.</summary>
internal static partial class ExecutableInfo
{
    /// <summary>Product names that engines stamp on every game they build.</summary>
    private static readonly HashSet<string> GenericProductNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "UnrealGame", "Unreal Engine", "UE4Game", "UE5Game", "Unity", "Unity Player", "BootstrapPackagedGame",
        "Godot Engine", "Microsoft® Windows® Operating System", "Game",
    };

    /// <summary>Folder names that describe a build layout, not the game.</summary>
    private static readonly HashSet<string> LayoutFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "bin32", "bin64", "binaries", "win32", "win64", "x86", "x64", "x86_64", "game", "release",
        "retail", "shipping", "build", "content", "data", "live", "app", "windows", "windowsnoeditor",
    };

    public static string GuessGameName(string executablePath)
    {
        if (ProductName(executablePath) is { } product)
            return product;

        // Walk up from the executable, skipping layout folders: D:\Jogos\Elden.Ring-RUNE\Game\eldenring.exe → "Elden Ring".
        var directory = PathUtil.DirectoryOf(executablePath);
        while (directory is not null && Path.GetFileName(directory) is { Length: > 0 } folder)
        {
            if (!LayoutFolders.Contains(folder))
                return CleanFolderName(folder);
            directory = PathUtil.DirectoryOf(directory);
        }
        return PathUtil.FileNameWithoutExtension(executablePath);
    }

    private static string? ProductName(string executablePath)
    {
        try
        {
            var name = FileVersionInfo.GetVersionInfo(executablePath).ProductName;
            name = name?.Replace("™", "", StringComparison.Ordinal).Replace("®", "", StringComparison.Ordinal).Trim();
            return string.IsNullOrEmpty(name) || GenericProductNames.Contains(name) ? null : name;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    /// <summary>"Elden.Ring-RUNE" / "Hollow_Knight [v1.5] (GOG)" → "Elden Ring" / "Hollow Knight".</summary>
    private static string CleanFolderName(string folder)
    {
        var name = BracketedPattern().Replace(folder, " ");

        // Only dotted/underscored names are release-style; "Half-Life" or "Spider-Man" keep their hyphen.
        if (name.Contains('.') || name.Contains('_'))
            name = ReleaseGroupSuffixPattern().Replace(name, "").Replace('.', ' ').Replace('_', ' ');

        name = WhitespacePattern().Replace(name, " ").Trim();
        return name.Length > 0 ? name : folder;
    }

    [GeneratedRegex(@"[\[\(\{].*?[\]\)\}]")]
    private static partial Regex BracketedPattern();

    /// <summary>Scene-style suffix: "-CODEX", "-RUNE", "-FitGirl"...</summary>
    [GeneratedRegex(@"-[A-Za-z0-9]+$")]
    private static partial Regex ReleaseGroupSuffixPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
