using GameTracker.Core.Detection;
using GameTracker.Core.Domain;
using GameTracker.Core.Metadata;
using GameTracker.Core.Settings;
using GameTracker.Core.Tracking;
using Microsoft.Extensions.Time.Testing;

namespace GameTracker.Core.Tests;

public class SessionTrackerTests
{
    private static readonly DetectedGame Doom = new("steam:782330", "DOOM Eternal", GameSource.Steam, "782330", null, @"C:\doom.exe");

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 8, 20, 0, 0, TimeSpan.Zero));
    private readonly InMemoryTrackingStore _store = new();
    private readonly FakeSettings _settings = new();
    private readonly FakeMetadataQueue _metadata = new();
    private readonly SessionTracker _tracker;

    public SessionTrackerTests() => _tracker = new SessionTracker(_store, _settings, _metadata, _time);

    private Task Tick(params DetectedGame[] games) => _tracker.ProcessAsync(games, CancellationToken.None);

    [Fact]
    public async Task Records_a_session_from_start_to_last_seen()
    {
        await Tick(Doom);
        _time.Advance(TimeSpan.FromMinutes(30));
        await Tick(Doom);
        _time.Advance(TimeSpan.FromSeconds(5));
        await Tick();

        var session = Assert.Single(_store.Sessions);
        Assert.False(session.IsOpen);
        Assert.Equal(TimeSpan.FromMinutes(30), session.Duration);
        Assert.Empty(_tracker.ActiveGames);
    }

    [Fact]
    public async Task New_game_is_created_once_and_sent_for_metadata()
    {
        await Tick(Doom);
        await Tick(Doom, Doom);

        Assert.Single(_store.Games);
        Assert.Equal([_store.Games[0].Id], _metadata.Queued);
        Assert.Single(_tracker.ActiveGames);
    }

    [Fact]
    public async Task Short_sessions_are_discarded_with_their_empty_game()
    {
        await Tick(Doom);
        _time.Advance(TimeSpan.FromSeconds(20));
        await Tick(Doom);
        await Tick();

        Assert.Empty(_store.Sessions);
        Assert.Empty(_store.Games);
    }

    [Fact]
    public async Task Reopening_within_merge_gap_continues_the_session()
    {
        await Tick(Doom);
        _time.Advance(TimeSpan.FromMinutes(10));
        await Tick(Doom);
        await Tick();                                // closed
        _time.Advance(TimeSpan.FromSeconds(60));     // within the 2 min gap
        await Tick(Doom);
        _time.Advance(TimeSpan.FromMinutes(5));
        await Tick(Doom);
        await Tick();

        var session = Assert.Single(_store.Sessions);
        Assert.Equal(TimeSpan.FromMinutes(16), session.Duration);
    }

    [Fact]
    public async Task Heartbeat_persists_progress_of_open_sessions()
    {
        await Tick(Doom);
        _time.Advance(TimeSpan.FromMinutes(3));
        await Tick(Doom);

        var session = Assert.Single(_store.Sessions);
        Assert.True(session.IsOpen);
        Assert.Equal(TimeSpan.FromMinutes(3), session.Duration);
    }

    [Fact]
    public async Task Ignored_games_are_not_tracked_and_active_sessions_end()
    {
        await Tick(Doom);
        _time.Advance(TimeSpan.FromMinutes(5));
        await Tick(Doom);

        _store.Games[0].IsIgnored = true;
        _tracker.ResetCache();
        await Tick(Doom);

        Assert.Empty(_tracker.ActiveGames);
        Assert.False(Assert.Single(_store.Sessions).IsOpen);
    }

    [Fact]
    public async Task Stop_closes_running_sessions_at_current_time()
    {
        await Tick(Doom);
        _time.Advance(TimeSpan.FromMinutes(2));
        await _tracker.StopAsync(CancellationToken.None);

        var session = Assert.Single(_store.Sessions);
        Assert.False(session.IsOpen);
        Assert.Equal(TimeSpan.FromMinutes(2), session.Duration);
    }

    [Fact]
    public async Task Suggestion_is_announced_only_after_minimum_duration()
    {
        var unknown = new DetectedGame(@"exe:d:\jogos\x\x.exe", "X", GameSource.Detected, null, @"D:\Jogos\X", @"D:\Jogos\X\x.exe");
        var announced = new List<ActiveGame>();
        _tracker.GameSuggested += (_, game) => announced.Add(game);

        await Tick(unknown);
        _time.Advance(TimeSpan.FromSeconds(30));
        await Tick(unknown);
        Assert.Empty(announced);

        _time.Advance(TimeSpan.FromSeconds(30));
        await Tick(unknown);
        await Tick(unknown);

        Assert.Equal("X", Assert.Single(announced).Name);
    }

    [Fact]
    public async Task Short_lived_suggestion_is_never_announced()
    {
        var unknown = new DetectedGame(@"exe:d:\jogos\x\x.exe", "X", GameSource.Detected, null, @"D:\Jogos\X", @"D:\Jogos\X\x.exe");
        var announced = 0;
        _tracker.GameSuggested += (_, _) => announced++;

        await Tick(unknown);
        _time.Advance(TimeSpan.FromSeconds(10));
        await Tick();

        Assert.Equal(0, announced);
        Assert.Empty(_store.Games);
    }

    private sealed class FakeSettings : ISettingsStore
    {
        public AppSettings Current { get; } = new();
        public event EventHandler<AppSettings>? Changed { add { } remove { } }
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMetadataQueue : IMetadataQueue
    {
        public List<int> Queued { get; } = [];
        public event EventHandler<int>? GameUpdated { add { } remove { } }
        public void Enqueue(int gameId) => Queued.Add(gameId);
    }
}
