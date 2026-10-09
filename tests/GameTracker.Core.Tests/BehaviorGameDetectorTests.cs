using GameTracker.Core.Detection;
using GameTracker.Core.Domain;
using Microsoft.Extensions.Time.Testing;

namespace GameTracker.Core.Tests;

public class BehaviorGameDetectorTests
{
    private const int GamePid = 4242;
    private const string GamePath = @"D:\Jogos\Elden.Ring-RUNE\Game\eldenring.exe";

    private readonly FakeProbe _probe = new() { ForegroundPid = GamePid, GamePids = { GamePid } };
    private readonly FakeTimeProvider _time = new();
    private readonly BehaviorGameDetector _detector;
    private RunningProcess[] _snapshot = [new(GamePid, "eldenring", GamePath)];

    public BehaviorGameDetectorTests() => _detector = new BehaviorGameDetector(_probe, _time);

    private bool Detect(out DetectedGame? game, Func<int, bool>? isKnown = null) =>
        _detector.TryDetect(_snapshot, isKnown ?? (_ => false), out _, out game);

    [Fact]
    public void Suggests_fullscreen_foreground_process_with_game_runtime()
    {
        Assert.True(Detect(out var game));
        Assert.Equal(GameSource.Detected, game!.Source);
        Assert.Equal(@"exe:d:\jogos\elden.ring-rune\game\eldenring.exe", game.Key);
        Assert.Equal("Display name", game.Name);
    }

    [Fact]
    public void Nothing_when_foreground_is_not_fullscreen()
    {
        _probe.ForegroundPid = null;
        Assert.False(Detect(out _));
    }

    [Fact]
    public void Nothing_when_process_is_already_known() => Assert.False(Detect(out _, isKnown: pid => pid == GamePid));

    [Theory]
    [InlineData("chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe")]
    [InlineData("vlc", @"C:\Program Files\VideoLAN\VLC\vlc.exe")]
    [InlineData("anything", @"C:\Windows\System32\anything.exe")]
    public void Never_suggests_known_non_games(string name, string path)
    {
        _snapshot = [new(GamePid, name, path)];
        Assert.False(Detect(out _));
    }

    [Fact]
    public void Process_without_game_runtime_is_rechecked_only_after_interval()
    {
        _probe.GamePids.Clear();
        Assert.False(Detect(out _));
        Assert.Equal(1, _probe.RuntimeChecks);

        _probe.GamePids.Add(GamePid); // runtime loaded later
        _time.Advance(TimeSpan.FromSeconds(5));
        Assert.False(Detect(out _));
        Assert.Equal(1, _probe.RuntimeChecks); // cached

        _time.Advance(BehaviorGameDetector.RecheckInterval);
        Assert.True(Detect(out _));
    }

    [Fact]
    public void Remembered_suggestion_is_matched_by_rules_on_next_launch()
    {
        Assert.True(Detect(out var game));
        var rules = new GameDetector();
        rules.Remember(game!);

        Assert.Equal(game, rules.Match(new RunningProcess(1, "eldenring", @"d:\JOGOS\Elden.Ring-RUNE\Game\eldenring.exe")));
    }

    private sealed class FakeProbe : IForegroundAppProbe
    {
        public int? ForegroundPid { get; set; }
        public HashSet<int> GamePids { get; } = [];
        public int RuntimeChecks { get; private set; }

        public int? GetFullscreenForegroundProcessId() => ForegroundPid;

        public bool HasGameRuntime(int processId)
        {
            RuntimeChecks++;
            return GamePids.Contains(processId);
        }

        public string GetDisplayName(string executablePath) => "Display name";
    }
}
