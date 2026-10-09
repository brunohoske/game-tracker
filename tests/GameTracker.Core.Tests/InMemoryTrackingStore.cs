using GameTracker.Core.Detection;
using GameTracker.Core.Domain;
using GameTracker.Core.Tracking;

namespace GameTracker.Core.Tests;

/// <summary>Mirrors the behaviour of the EF store, in memory.</summary>
internal sealed class InMemoryTrackingStore : ITrackingStore
{
    public List<Game> Games { get; } = [];
    public List<GameSession> Sessions { get; } = [];

    public Task CloseOrphanedSessionsAsync(CancellationToken cancellationToken)
    {
        Sessions.ForEach(s => s.IsOpen = false);
        return Task.CompletedTask;
    }

    public Task<TrackedGame> GetOrCreateGameAsync(DetectedGame detected, CancellationToken cancellationToken)
    {
        if (Games.FirstOrDefault(g => g.Key == detected.Key) is { } existing)
            return Task.FromResult(new TrackedGame(existing.Id, existing.IsIgnored, false));

        var game = new Game { Id = Games.Count + 1, Key = detected.Key, Name = detected.Name, Source = detected.Source };
        Games.Add(game);
        return Task.FromResult(new TrackedGame(game.Id, false, true));
    }

    public Task<long> StartSessionAsync(int gameId, DateTime nowUtc, TimeSpan mergeGap, CancellationToken cancellationToken)
    {
        var session = Sessions
            .Where(s => s.GameId == gameId && !s.IsOpen && s.EndedAtUtc >= nowUtc - mergeGap)
            .MaxBy(s => s.EndedAtUtc);
        if (session is null)
        {
            session = new GameSession { Id = Sessions.Count + 1, GameId = gameId, StartedAtUtc = nowUtc };
            Sessions.Add(session);
        }
        session.EndedAtUtc = nowUtc;
        session.IsOpen = true;
        return Task.FromResult(session.Id);
    }

    public Task TouchSessionsAsync(IReadOnlyCollection<long> sessionIds, DateTime nowUtc, CancellationToken cancellationToken)
    {
        foreach (var session in Sessions.Where(s => sessionIds.Contains(s.Id)))
            session.EndedAtUtc = nowUtc;
        return Task.CompletedTask;
    }

    public Task<SessionEndResult> EndSessionAsync(long sessionId, DateTime endedAtUtc, TimeSpan minimumDuration, CancellationToken cancellationToken)
    {
        var session = Sessions.Single(s => s.Id == sessionId);
        session.IsOpen = false;
        session.EndedAtUtc = endedAtUtc;
        if (session.Duration >= minimumDuration)
            return Task.FromResult(SessionEndResult.Saved);

        Sessions.Remove(session);
        if (Sessions.Any(s => s.GameId == session.GameId))
            return Task.FromResult(SessionEndResult.Discarded);

        Games.RemoveAll(g => g.Id == session.GameId);
        return Task.FromResult(SessionEndResult.DiscardedWithGame);
    }

    public Task<IReadOnlyList<DetectedGame>> GetKnownGamesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DetectedGame>>([]);
}
