using GameTracker.Core.Detection;
using GameTracker.Core.Domain;

namespace GameTracker.Core.Tests;

public class GameDetectorTests
{
    private readonly GameDetector _detector = new();

    private static RunningProcess Process(string path) =>
        new(1234, Path.GetFileNameWithoutExtension(path), path);

    [Fact]
    public void Matches_steam_library_entry_by_install_folder()
    {
        _detector.UpdateLibrary([new LibraryEntry(GameSource.Steam, "1091500", "Cyberpunk 2077", @"D:\SteamLibrary\steamapps\common\Cyberpunk 2077")]);

        var match = _detector.Match(Process(@"D:\SteamLibrary\steamapps\common\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe"));

        Assert.NotNull(match);
        Assert.Equal("steam:1091500", match.Key);
        Assert.Equal("Cyberpunk 2077", match.Name);
        Assert.Equal(GameSource.Steam, match.Source);
    }

    [Fact]
    public void Does_not_match_sibling_folder_with_common_prefix()
    {
        _detector.UpdateLibrary([new LibraryEntry(GameSource.Epic, "Game", "Game", @"C:\Games\Game")]);

        Assert.Null(_detector.Match(Process(@"C:\Games\Game2\Game2.exe")));
    }

    [Fact]
    public void Most_specific_library_folder_wins()
    {
        _detector.UpdateLibrary(
        [
            new LibraryEntry(GameSource.Epic, "parent", "Parent", @"C:\Epic"),
            new LibraryEntry(GameSource.Epic, "child", "Child", @"C:\Epic\Child"),
        ]);

        Assert.Equal("epic:child", _detector.Match(Process(@"C:\Epic\Child\run.exe"))?.Key);
    }

    [Theory]
    [InlineData(@"D:\SteamLibrary\steamapps\common\Game\UnityCrashHandler64.exe")]
    [InlineData(@"D:\SteamLibrary\steamapps\common\Game\EasyAntiCheat\EasyAntiCheat_Setup.exe")]
    [InlineData(@"D:\SteamLibrary\steamapps\common\Game\unins000.exe")]
    public void Ignores_helper_executables(string path) => Assert.Null(_detector.Match(Process(path)));

    [Fact]
    public void Recognises_conventional_game_folders()
    {
        var match = _detector.Match(Process(@"E:\XboxGames\Forza Horizon 5\Content\ForzaHorizon5.exe"));

        Assert.NotNull(match);
        Assert.Equal("Forza Horizon 5", match.Name);
        Assert.Equal(GameSource.Folder, match.Source);
        Assert.Equal(@"dir:e:\xboxgames\forza horizon 5", match.Key);
    }

    [Fact]
    public void Skips_launchers_inside_game_folders()
    {
        Assert.Null(_detector.Match(Process(@"C:\Riot Games\Riot Client\RiotClientServices.exe")));
        Assert.Null(_detector.Match(Process(@"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe")));
    }

    [Fact]
    public void Excluded_steam_tools_are_not_games()
    {
        _detector.UpdateLibrary([new LibraryEntry(GameSource.Steam, "431960", "Wallpaper Engine", @"C:\Steam\steamapps\common\wallpaper_engine")]);

        Assert.Null(_detector.Match(Process(@"C:\Steam\steamapps\common\wallpaper_engine\wallpaper64.exe")));
    }

    [Fact]
    public void Uses_user_folders()
    {
        _detector.UpdateUserFolders([@"D:\Jogos"]);

        Assert.Equal("Hollow Knight", _detector.Match(Process(@"D:\Jogos\Hollow Knight\hollow_knight.exe"))?.Name);
    }

    [Fact]
    public void Ignores_regular_applications()
    {
        Assert.Null(_detector.Match(Process(@"C:\Program Files\Mozilla Firefox\firefox.exe")));
        Assert.Null(_detector.Match(Process(@"C:\Windows\explorer.exe")));
    }

    [Fact]
    public void Manual_executables_take_precedence()
    {
        var manual = new DetectedGame(@"exe:c:\tools\emu.exe", "Emulador", GameSource.Manual, null, @"C:\Tools", @"C:\Tools\emu.exe");
        _detector.UpdateKnownGames([manual]);

        Assert.Equal(manual, _detector.Match(Process(@"C:\TOOLS\EMU.EXE")));
    }

    [Fact]
    public void Falls_back_to_executable_name_when_path_is_unavailable()
    {
        var seen = _detector.Match(Process(@"C:\Riot Games\VALORANT\live\VALORANT.exe"));

        var protectedProcess = _detector.Match(new RunningProcess(99, "VALORANT", ExecutablePath: null));

        Assert.NotNull(seen);
        Assert.Equal(seen, protectedProcess);
    }
}
