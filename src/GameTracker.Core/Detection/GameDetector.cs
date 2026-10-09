using GameTracker.Core.Common;
using GameTracker.Core.Domain;

namespace GameTracker.Core.Detection;

/// <summary>
/// Decides whether a running process is a game. Rules, in order of precedence:
/// <list type="number">
///   <item>Executables bound to a single game: added manually or detected by behaviour.</item>
///   <item>Helpers (crash reporters, anti-cheat, installers...) are never games.</item>
///   <item>Processes inside the install folder of a launcher library entry (Steam, Epic, GOG...).</item>
///   <item>Processes inside a conventional game folder (e.g. <c>XboxGames\{Game}</c>) or a user folder.</item>
///   <item>When the path cannot be read (protected process), the executable name of a game seen before.</item>
/// </list>
/// Not thread-safe: owned by the tracking loop.
/// </summary>
public sealed class GameDetector
{
    private IndexedEntry[] _library = [];
    private string[] _folderMarkers = [.. DetectionDefaults.GameFolderMarkers];
    private Dictionary<string, DetectedGame> _exactExecutables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DetectedGame> _knownByExecutableName = new(StringComparer.OrdinalIgnoreCase);

    public void UpdateLibrary(IEnumerable<LibraryEntry> entries)
    {
        _library =
        [
            .. entries
                .Where(e => !(e.Source == GameSource.Steam && DetectionDefaults.NonGameSteamAppIds.Contains(e.ExternalId)))
                .Select(e => new IndexedEntry(PathUtil.Normalize(e.InstallDirectory), e))
                .Where(e => e.NormalizedDirectory.Length > 3) // never a drive root
                .OrderByDescending(e => e.NormalizedDirectory.Length), // most specific folder wins
        ];
    }

    /// <summary>Folders chosen by the user where each sub-folder is a game.</summary>
    public void UpdateUserFolders(IEnumerable<string> folders)
    {
        _folderMarkers =
        [
            .. folders.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim().TrimEnd('\\', '/') + "\\"),
            .. DetectionDefaults.GameFolderMarkers,
        ];
    }

    /// <summary>Games already in the database: enables exact executables and the name-only fallback.</summary>
    public void UpdateKnownGames(IEnumerable<DetectedGame> games)
    {
        _exactExecutables = new Dictionary<string, DetectedGame>(StringComparer.Ordinal);
        foreach (var game in games)
            Remember(game);
    }

    /// <summary>Learns a game found outside the rules (e.g. by behaviour) so it is recognised directly next time.</summary>
    public void Remember(DetectedGame game)
    {
        if (game.ExecutablePath is null)
            return;
        if (game.Source is GameSource.Manual or GameSource.Detected)
            _exactExecutables[PathUtil.Normalize(game.ExecutablePath)] = game;
        _knownByExecutableName[PathUtil.FileNameWithoutExtension(game.ExecutablePath)] = game;
    }

    public DetectedGame? Match(RunningProcess process)
    {
        if (process.ExecutablePath is not { } path)
            return _knownByExecutableName.GetValueOrDefault(process.Name);

        var normalized = PathUtil.Normalize(path);
        if (_exactExecutables.TryGetValue(normalized, out var exact))
            return exact;

        if (IsHelperExecutable(normalized))
            return null;

        var match = MatchLibrary(normalized, path) ?? MatchFolder(path);
        if (match is not null)
            _knownByExecutableName.TryAdd(PathUtil.FileNameWithoutExtension(path), match);
        return match;
    }

    private DetectedGame? MatchLibrary(string normalizedPath, string path)
    {
        foreach (var (directory, entry) in _library)
        {
            if (PathUtil.IsUnder(normalizedPath, directory))
                return new DetectedGame(entry.Key, entry.Name, entry.Source, entry.ExternalId, entry.InstallDirectory, path);
        }
        return null;
    }

    private DetectedGame? MatchFolder(string path)
    {
        foreach (var marker in _folderMarkers)
        {
            var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                continue;

            var gameStart = index + marker.Length;
            var gameEnd = path.IndexOf('\\', gameStart);
            if (gameEnd <= gameStart)
                continue; // the executable sits directly in the marker folder

            var folderName = path[gameStart..gameEnd];
            if (DetectionDefaults.NonGameFolders.Contains(folderName))
                return null;

            var root = path[..gameEnd];
            return new DetectedGame(GameKeys.ForFolder(root), folderName, GameSource.Folder, null, root, path);
        }
        return null;
    }

    private static bool IsHelperExecutable(string normalizedPath)
    {
        var name = PathUtil.FileNameWithoutExtension(normalizedPath);
        foreach (var fragment in DetectionDefaults.NonGameExecutableFragments)
        {
            if (name.Contains(fragment, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private readonly record struct IndexedEntry(string NormalizedDirectory, LibraryEntry Entry);
}
