using GameTracker.Core.Detection;
using GameTracker.Core.Domain;
using GameTracker.Core.Tracking;
using GameTracker.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace GameTracker.Infrastructure.Persistence;

internal sealed class EfTrackingStore(
    IDbContextFactory<GameTrackerDbContext> contextFactory,
    ImageStore images,
    TimeProvider time) : ITrackingStore
{
    public async Task CloseOrphanedSessionsAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Sessions
            .Where(s => s.IsOpen)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.IsOpen, false), cancellationToken);
    }

    public async Task<TrackedGame> GetOrCreateGameAsync(DetectedGame detected, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.Games
            .AsNoTracking()
            .Where(g => g.Key == detected.Key)
            .Select(g => new TrackedGame(g.Id, g.IsIgnored, false))
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
            return existing;

        var game = new Game
        {
            Key = detected.Key,
            Name = detected.Name,
            Source = detected.Source,
            ExternalId = detected.ExternalId,
            InstallDirectory = detected.InstallDirectory,
            ExecutablePath = detected.ExecutablePath,
            IsSuggested = detected.Source == GameSource.Detected,
            AddedAtUtc = time.GetUtcNow().UtcDateTime,
        };
        db.Games.Add(game);
        await db.SaveChangesAsync(cancellationToken);
        return new TrackedGame(game.Id, false, true);
    }

    public async Task<long> StartSessionAsync(int gameId, DateTime nowUtc, TimeSpan mergeGap, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var mergeThreshold = nowUtc - mergeGap;
        var recent = await db.Sessions
            .Where(s => s.GameId == gameId && !s.IsOpen && s.EndedAtUtc >= mergeThreshold)
            .OrderByDescending(s => s.EndedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var session = recent ?? db.Sessions.Add(new GameSession { GameId = gameId, StartedAtUtc = nowUtc }).Entity;
        session.EndedAtUtc = nowUtc;
        session.IsOpen = true;

        await db.SaveChangesAsync(cancellationToken);
        return session.Id;
    }

    public async Task TouchSessionsAsync(IReadOnlyCollection<long> sessionIds, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Sessions
            .Where(s => sessionIds.Contains(s.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.EndedAtUtc, nowUtc), cancellationToken);
    }

    public async Task<SessionEndResult> EndSessionAsync(long sessionId, DateTime endedAtUtc, TimeSpan minimumDuration, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var session = await db.Sessions.FindAsync([sessionId], cancellationToken);
        if (session is null)
            return SessionEndResult.Discarded;

        session.IsOpen = false;
        session.EndedAtUtc = endedAtUtc > session.StartedAtUtc ? endedAtUtc : session.StartedAtUtc;

        if (session.Duration >= minimumDuration)
        {
            await db.SaveChangesAsync(cancellationToken);
            return SessionEndResult.Saved;
        }

        db.Sessions.Remove(session);
        await db.SaveChangesAsync(cancellationToken);

        // A false positive (tool opened for a few seconds) should not leave an empty game behind.
        var removedGames = await db.Games
            .Where(g => g.Id == session.GameId
                        && g.Source != GameSource.Manual
                        && !g.IsIgnored
                        && !g.Sessions.Any())
            .ExecuteDeleteAsync(cancellationToken);

        if (removedGames == 0)
            return SessionEndResult.Discarded;

        images.DeleteGameImages(session.GameId);
        return SessionEndResult.DiscardedWithGame;
    }

    public async Task<IReadOnlyList<DetectedGame>> GetKnownGamesAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Games
            .AsNoTracking()
            .Where(g => g.ExecutablePath != null)
            .Select(g => new DetectedGame(g.Key, g.Name, g.Source, g.ExternalId, g.InstallDirectory, g.ExecutablePath))
            .ToListAsync(cancellationToken);
    }
}
