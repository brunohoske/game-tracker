using GameTracker.Core.Common;
using GameTracker.Core.Domain;
using GameTracker.Core.Library;
using GameTracker.Core.Metadata;
using GameTracker.Core.Reporting;
using GameTracker.Core.Tracking;
using GameTracker.Infrastructure.Detection;
using GameTracker.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace GameTracker.Infrastructure.Persistence;

internal sealed class GameLibrary(
    IDbContextFactory<GameTrackerDbContext> contextFactory,
    ILibraryChangeNotifier changeNotifier,
    IMetadataQueue metadataQueue,
    ImageStore images,
    TimeProvider time) : IGameLibrary
{
    public async Task<int> AddManualGameAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var key = GameKeys.ForExecutable(executablePath);
        var game = await db.Games.FirstOrDefaultAsync(g => g.Key == key, cancellationToken);
        if (game is null)
        {
            game = new Game
            {
                Key = key,
                Name = ExecutableInfo.GuessGameName(executablePath),
                Source = GameSource.Manual,
                ExecutablePath = executablePath,
                InstallDirectory = PathUtil.DirectoryOf(executablePath),
                AddedAtUtc = time.GetUtcNow().UtcDateTime,
            };
            db.Games.Add(game);
        }
        game.IsIgnored = false;
        game.IsSuggested = false;
        await db.SaveChangesAsync(cancellationToken);

        changeNotifier.NotifyLibraryChanged();
        if (game.MetadataUpdatedAtUtc is null)
            metadataQueue.Enqueue(game.Id);
        return game.Id;
    }

    public async Task RenameAsync(int gameId, string name, CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        if (name.Length == 0)
            return;

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var updated = await db.Games
            .Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(g => g.Name, name), cancellationToken);

        if (updated > 0)
        {
            changeNotifier.NotifyLibraryChanged();
            metadataQueue.Enqueue(gameId); // a better name usually means better metadata
        }
    }

    public async Task SetIgnoredAsync(int gameId, bool ignored, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Either answer settles a pending suggestion: ignored = "not a game", restored = a regular game.
        await db.Games
            .Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(g => g.IsIgnored, ignored)
                .SetProperty(g => g.IsSuggested, false), cancellationToken);
        changeNotifier.NotifyLibraryChanged();
    }

    public async Task<IReadOnlyList<GameSuggestion>> GetSuggestionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var suggestions = await db.Games
            .AsNoTracking()
            .Where(g => g.IsSuggested && !g.IsIgnored)
            .OrderByDescending(g => g.AddedAtUtc)
            .Select(g => new { Game = g, Sessions = g.Sessions.Select(s => new { s.StartedAtUtc, s.EndedAtUtc, s.IsOpen }).ToList() })
            .ToListAsync(cancellationToken);

        var now = time.GetUtcNow().UtcDateTime;
        return
        [
            .. suggestions.Select(s => new GameSuggestion(
                ReportService.ToInfo(s.Game),
                s.Sessions.Aggregate(TimeSpan.Zero, (sum, x) => sum + ((x.IsOpen && x.EndedAtUtc < now ? now : x.EndedAtUtc) - x.StartedAtUtc)))),
        ];
    }

    public async Task ConfirmSuggestionAsync(int gameId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Games
            .Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(g => g.IsSuggested, false), cancellationToken);
    }

    public async Task DeleteAsync(int gameId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Games.Where(g => g.Id == gameId).ExecuteDeleteAsync(cancellationToken);
        images.DeleteGameImages(gameId);
        changeNotifier.NotifyLibraryChanged();
    }

    public void RefreshMetadata(int gameId) => metadataQueue.Enqueue(gameId);

    public async Task<IReadOnlyList<GameInfo>> GetIgnoredGamesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var games = await db.Games.AsNoTracking().Where(g => g.IsIgnored).OrderBy(g => g.Name).ToListAsync(cancellationToken);
        return [.. games.Select(ReportService.ToInfo)];
    }
}
