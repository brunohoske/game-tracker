using GameTracker.Core.Domain;

namespace GameTracker.Core.Detection;

/// <param name="ExecutablePath">Null when the process cannot be queried (e.g. protected by an anti-cheat).</param>
public sealed record RunningProcess(int ProcessId, string Name, string? ExecutablePath);

/// <summary>A game installed through a launcher (Steam, Epic, GOG...).</summary>
public sealed record LibraryEntry(GameSource Source, string ExternalId, string Name, string InstallDirectory)
{
    public string Key => GameKeys.ForLibrary(Source, ExternalId);
}

/// <summary>A running process recognised as a game.</summary>
public sealed record DetectedGame(
    string Key,
    string Name,
    GameSource Source,
    string? ExternalId,
    string? InstallDirectory,
    string? ExecutablePath);

public interface IProcessSnapshotProvider
{
    IReadOnlyList<RunningProcess> GetRunningProcesses();
}

/// <summary>Inspects the foreground application (Win32 specifics live in the infrastructure).</summary>
public interface IForegroundAppProbe
{
    /// <summary>Process owning the foreground window when it covers its whole monitor (exclusive or borderless fullscreen).</summary>
    int? GetFullscreenForegroundProcessId();

    /// <summary>
    /// True when the loaded modules look like a game: a 3D API (Direct3D, Vulkan, OpenGL) <b>and</b> a game input
    /// library (XInput, DirectInput, GameInput) or game runtime (Unity, Steam/Epic SDK, FMOD, SDL...).
    /// A 3D API alone is not enough: modern Windows apps (Explorer, Notepad, Spotify...) all load Direct3D.
    /// </summary>
    bool HasGameRuntime(int processId);

    /// <summary>Best human-readable name for an executable (product name, falling back to its folder).</summary>
    string GetDisplayName(string executablePath);
}

/// <summary>Reads the list of installed games from a launcher.</summary>
public interface ILibraryScanner
{
    string Name { get; }

    IReadOnlyList<LibraryEntry> Scan();
}
