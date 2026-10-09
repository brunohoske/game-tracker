using System.Text.Json;
using GameTracker.Core.Detection;
using GameTracker.Core.Domain;

namespace GameTracker.Infrastructure.Detection.Libraries;

/// <summary>Reads the Epic Games Launcher install manifests (*.item JSON files).</summary>
internal sealed class EpicLibraryScanner : ILibraryScanner
{
    public string Name => "Epic Games";

    public IReadOnlyList<LibraryEntry> Scan()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Epic\EpicGamesLauncher\Data\Manifests");
        if (!Directory.Exists(directory))
            return [];

        var entries = new List<LibraryEntry>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.item"))
        {
            var manifest = JsonSerializer.Deserialize<EpicManifest>(File.ReadAllText(file));
            if (manifest is { AppName: { Length: > 0 } appName, DisplayName: { Length: > 0 } displayName, InstallLocation: { Length: > 0 } location }
                && !manifest.IsIncompleteInstall
                && (manifest.AppCategories is null || manifest.AppCategories.Contains("games", StringComparer.OrdinalIgnoreCase)))
            {
                entries.Add(new LibraryEntry(GameSource.Epic, appName, displayName, location));
            }
        }
        return entries;
    }

    private sealed record EpicManifest(
        string? AppName,
        string? DisplayName,
        string? InstallLocation,
        string[]? AppCategories,
        [property: System.Text.Json.Serialization.JsonPropertyName("bIsIncompleteInstall")] bool IsIncompleteInstall);
}
