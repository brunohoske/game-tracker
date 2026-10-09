namespace GameTracker.Core.Detection;

/// <summary>Built-in knowledge used to tell games apart from everything else.</summary>
public static class DetectionDefaults
{
    /// <summary>
    /// Folders that conventionally hold one sub-folder per game. A process running from
    /// <c>{marker}\{Game}\...</c> is considered the game <c>{Game}</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> GameFolderMarkers =
    [
        @"\steamapps\common\",
        @"\Epic Games\",
        @"\GOG Galaxy\Games\",
        @"\GOG Games\",
        @"\XboxGames\",
        @"\Riot Games\",
        @"\Ubisoft Game Launcher\games\",
        @"\EA Games\",
        @"\Rockstar Games\",
        @"\Amazon Games\Library\",
    ];

    /// <summary>Sub-folders of the markers above that are launchers or tools, not games.</summary>
    public static readonly IReadOnlySet<string> NonGameFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Riot Client", "Launcher", "Epic Online Services", "DirectXRedist", "Steamworks Shared",
        "Steam Controller Configs", "CommonRedist", "Social Club", "Rockstar Games Launcher",
        "wallpaper_engine", "SteamVR", "Lossless Scaling",
    };

    /// <summary>Steam apps that are tools or run in background (never games).</summary>
    public static readonly IReadOnlySet<string> NonGameSteamAppIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "228980",  // Steamworks Common Redistributables
        "250820",  // SteamVR
        "431960",  // Wallpaper Engine
        "993090",  // Lossless Scaling
        "1070560", // Steam Linux Runtime
        "1391110", // Steam Linux Runtime - Soldier
        "1628350", // Steam Linux Runtime - Sniper
        "480",     // Spacewar (SDK sample)
    };

    /// <summary>
    /// Applications that commonly run fullscreen with a 3D API but are not games (browsers, players, streaming,
    /// creative tools, launchers). They are never suggested.
    /// </summary>
    public static readonly IReadOnlySet<string> NonGameFullscreenApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Browsers
        "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "iexplore", "arc", "zen",
        // Video players & streaming
        "vlc", "mpc-hc", "mpc-hc64", "mpc-be", "mpc-be64", "mpv", "potplayer", "potplayermini", "potplayermini64",
        "wmplayer", "kodi", "plex", "stremio", "obs64", "obs32", "streamlabs obs",
        // Communication & remote access
        "discord", "teams", "ms-teams", "zoom", "slack", "mstsc", "parsecd", "moonlight", "anydesk", "teamviewer",
        // Launchers & shells
        "steam", "steamwebhelper", "epicgameslauncher", "galaxyclient", "upc", "eadesktop", "battle.net", "riotclientux",
        "explorer", "applicationframehost", "searchhost", "shellexperiencehost",
        // Overlays (they carry game libraries but are not the game)
        "eosoverlayrenderer-win64-shipping", "eosoverlayrenderer-win32-shipping", "gameoverlayui", "gameoverlayui64",
        "edgegameassist", "gamebar", "nvidia overlay", "radeonsoftware",
        // Creative & development tools
        "blender", "resolve", "adobe premiere pro", "afterfx", "photoshop", "illustrator", "unity", "unrealeditor",
        "godot", "devenv", "code", "rider64", "powerpnt", "vmware", "vmware-vmx", "virtualboxvm",
    };

    /// <summary>Executable name fragments of helpers that live inside game folders (lower case).</summary>
    public static readonly IReadOnlyList<string> NonGameExecutableFragments =
    [
        "crash", "unins", "setup", "redist", "prereq", "installer", "anticheat", "battleye", "beservice",
        "webhelper", "updater", "reporter", "cefsharp", "qtwebengine", "dxsetup", "dotnet", "vrserver",
        "vrmonitor", "vrcompositor", "vrdashboard", "wallpaper32", "wallpaper64", "losslessscaling",
    ];
}
