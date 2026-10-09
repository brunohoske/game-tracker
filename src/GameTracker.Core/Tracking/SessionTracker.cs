using GameTracker.Core.Detection;
using GameTracker.Core.Domain;
using GameTracker.Core.Metadata;
using GameTracker.Core.Settings;

namespace GameTracker.Core.Tracking;

/// <summary>
/// Turns successive snapshots of running games into play sessions.
/// <see cref="ProcessAsync"/> must be called from a single loop; <see cref="ActiveGames"/> can be read from any thread.
/// </summary>
public sealed class SessionTracker(
    ITrackingStore store,
    ISettingsStore settings,
    IMetadataQueue metadataQueue,
    TimeProvider time) : IActiveGamesMonitor
{
    private readonly Dictionary<string, ActiveSession> _active = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TrackedGame> _games = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pendingSuggestions = new(StringComparer.Ordinal);
    private IReadOnlyCollection<ActiveGame> _activeGames = [];

    public IReadOnlyCollection<ActiveGame> ActiveGames => Volatile.Read(ref _activeGames);

    public event EventHandler? ActiveGamesChanged;

    public event EventHandler<ActiveGame>? GameSuggested;

    public Task InitializeAsync(CancellationToken cancellationToken) =>
        store.CloseOrphanedSessionsAsync(cancellationToken);

    /// <summary>Forgets cached game states so that changes made by the user (ignore, delete...) are picked up.</summary>
    public void ResetCache() => _games.Clear();

    public async Task ProcessAsync(IEnumerable<DetectedGame> detected, CancellationToken cancellationToken)
    {
        var options = TrackingOptions.From(settings.Current);
        var now = time.GetUtcNow().UtcDateTime;
        var running = new HashSet<string>(StringComparer.Ordinal);
        var changed = false;

        foreach (var game in detected)
        {
            if (running.Contains(game.Key))
                continue;

            var tracked = await GetTrackedGameAsync(game, cancellationToken);
            if (tracked.IsIgnored)
                continue;

            running.Add(game.Key);
            if (_active.TryGetValue(game.Key, out var session))
            {
                session.LastSeenUtc = now;
                continue;
            }

            var sessionId = await store.StartSessionAsync(tracked.GameId, now, options.MergeGap, cancellationToken);
            _active[game.Key] = new ActiveSession(tracked.GameId, sessionId, game.Name, now);
            changed = true;
        }

        foreach (var (key, session) in _active.ToArray())
        {
            if (running.Contains(key))
                continue;

            await EndAsync(key, session, session.LastSeenUtc, options, cancellationToken);
            changed = true;
        }

        await HeartbeatAsync(now, options, cancellationToken);
        AnnounceQualifiedSuggestions(now, options);

        if (changed)
            PublishActiveGames();
    }

    /// <summary>Ends every open session (application shutdown).</summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_active.Count == 0)
            return;

        var options = TrackingOptions.From(settings.Current);
        var now = time.GetUtcNow().UtcDateTime;
        foreach (var (key, session) in _active.ToArray())
            await EndAsync(key, session, now, options, cancellationToken);

        PublishActiveGames();
    }

    private async Task<TrackedGame> GetTrackedGameAsync(DetectedGame game, CancellationToken cancellationToken)
    {
        if (_games.TryGetValue(game.Key, out var cached))
            return cached;

        var tracked = await store.GetOrCreateGameAsync(game, cancellationToken);
        if (tracked.IsNew)
        {
            metadataQueue.Enqueue(tracked.GameId);
            if (game.Source == GameSource.Detected)
                _pendingSuggestions.Add(game.Key);
        }

        _games[game.Key] = tracked with { IsNew = false };
        return tracked;
    }

    private async Task EndAsync(string key, ActiveSession session, DateTime endedAtUtc, TrackingOptions options, CancellationToken cancellationToken)
    {
        var result = await store.EndSessionAsync(session.SessionId, endedAtUtc, options.MinimumSessionDuration, cancellationToken);
        if (result == SessionEndResult.DiscardedWithGame)
            _games.Remove(key);
        _active.Remove(key);
    }

    private async Task HeartbeatAsync(DateTime now, TrackingOptions options, CancellationToken cancellationToken)
    {
        var due = _active.Values.Where(s => now - s.LastPersistedUtc >= options.HeartbeatInterval).ToArray();
        if (due.Length == 0)
            return;

        await store.TouchSessionsAsync([.. due.Select(s => s.SessionId)], now, cancellationToken);
        foreach (var session in due)
            session.LastPersistedUtc = now;
    }

    /// <summary>
    /// A suggestion is announced only once its session outlives the minimum duration: a program that is
    /// fullscreen for a few seconds is discarded silently instead of producing a notification.
    /// </summary>
    private void AnnounceQualifiedSuggestions(DateTime now, TrackingOptions options)
    {
        foreach (var key in _pendingSuggestions.ToArray())
        {
            if (!_active.TryGetValue(key, out var session))
            {
                _pendingSuggestions.Remove(key); // discarded before qualifying
            }
            else if (now - session.StartedAtUtc >= options.MinimumSessionDuration)
            {
                _pendingSuggestions.Remove(key);
                GameSuggested?.Invoke(this, new ActiveGame(session.GameId, session.Name, session.StartedAtUtc));
            }
        }
    }

    private void PublishActiveGames()
    {
        Volatile.Write(ref _activeGames, [.. _active.Values.Select(s => new ActiveGame(s.GameId, s.Name, s.StartedAtUtc))]);
        ActiveGamesChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class ActiveSession(int gameId, long sessionId, string name, DateTime startedAtUtc)
    {
        public int GameId { get; } = gameId;
        public long SessionId { get; } = sessionId;
        public string Name { get; } = name;
        public DateTime StartedAtUtc { get; } = startedAtUtc;
        public DateTime LastSeenUtc { get; set; } = startedAtUtc;
        public DateTime LastPersistedUtc { get; set; } = startedAtUtc;
    }
}
