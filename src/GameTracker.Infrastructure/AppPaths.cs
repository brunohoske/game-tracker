namespace GameTracker.Infrastructure;

/// <summary>
/// Locations of everything the application stores on disk: %LocalAppData%\GameTracker, or the folder in the
/// <c>GAMETRACKER_DATA_DIR</c> environment variable (handy for development with a separate database).
/// </summary>
public sealed class AppPaths
{
    public const string DataDirectoryVariable = "GAMETRACKER_DATA_DIR";

    public AppPaths(string? root = null)
    {
        Root = root
               ?? Environment.GetEnvironmentVariable(DataDirectoryVariable)
               ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameTracker");
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ImagesDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    public string Root { get; }
    public string DatabasePath => Path.Combine(Root, "gametracker.db");
    public string SettingsPath => Path.Combine(Root, "settings.json");
    public string ImagesDirectory => Path.Combine(Root, "images");
    public string LogsDirectory => Path.Combine(Root, "logs");

    public string GameImagesDirectory(int gameId) => Path.Combine(ImagesDirectory, gameId.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
