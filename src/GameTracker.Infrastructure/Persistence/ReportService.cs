using GameTracker.Core.Domain;
using GameTracker.Core.Reporting;
using Microsoft.EntityFrameworkCore;

namespace GameTracker.Infrastructure.Persistence;

/// <summary>
/// Reads sessions overlapping the period and aggregates them in memory: the data set of a single
/// player is small, and in-memory clipping keeps month boundaries exact.
/// </summary>
internal sealed class ReportService(IDbContextFactory<GameTrackerDbContext> contextFactory, TimeProvider time) : IReportService
{
    private const int MonthlyHistoryLength = 12;

    public async Task<LibraryReport> GetLibraryReportAsync(ReportPeriod period, CancellationToken cancellationToken = default)
    {
        var range = period.ToUtcRange(TimeZoneInfo.Local);
        var (sessions, games) = await LoadLibrarySessionsAsync(range, cancellationToken);

        var summaries = sessions
            .GroupBy(s => s.GameId)
            .Where(group => games.ContainsKey(group.Key))
            .Select(group =>
            {
                var game = games[group.Key];
                return new GameSummary(
                    game.Id,
                    game.Name,
                    game.CoverImagePath,
                    PlaytimeCalculator.Total(group, range),
                    group.Count(),
                    group.Max(s => s.EndedAtUtc));
            })
            .OrderByDescending(s => s.LastPlayedUtc)
            .ToList();

        // Games added manually but not played yet still belong to the library.
        if (range is null)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            var unplayed = await db.Games
                .AsNoTracking()
                .Where(g => !g.IsIgnored && !g.IsSuggested && !g.Sessions.Any())
                .Select(g => new GameSummary(g.Id, g.Name, g.CoverImagePath, TimeSpan.Zero, 0, null))
                .ToListAsync(cancellationToken);
            summaries.AddRange(unplayed);
        }

        return new LibraryReport(summaries, summaries.Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Playtime), sessions.Count);
    }

    public async Task<GameReport?> GetGameReportAsync(int gameId, ReportPeriod period, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var game = await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);
        if (game is null)
            return null;

        var all = WithLiveEnd(await ToSpans(db.Sessions.AsNoTracking().Where(s => s.GameId == gameId)).ToListAsync(cancellationToken));
        var range = period.ToUtcRange(TimeZoneInfo.Local);
        var inPeriod = all
            .Where(s => PlaytimeCalculator.Overlaps(s, range))
            .OrderByDescending(s => s.StartedAtUtc)
            .ToList();

        var playtime = PlaytimeCalculator.Total(inPeriod, range);
        var currentMonth = YearMonth.From(time.GetLocalNow().DateTime);

        return new GameReport(
            ToInfo(game),
            playtime,
            PlaytimeCalculator.Total(all, null),
            inPeriod.Count,
            inPeriod.Count == 0 ? TimeSpan.Zero : playtime / inPeriod.Count,
            inPeriod.Count == 0 ? TimeSpan.Zero : inPeriod.Max(s => s.Duration),
            all.Count == 0 ? null : all.Min(s => s.StartedAtUtc),
            all.Count == 0 ? null : all.Max(s => s.EndedAtUtc),
            inPeriod,
            PlaytimeCalculator.ByMonth(all, currentMonth, MonthlyHistoryLength, TimeZoneInfo.Local));
    }

    public async Task<IReadOnlyList<YearMonth>> GetActiveMonthsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var starts = await db.Sessions
            .AsNoTracking()
            .Where(s => !s.Game!.IsIgnored && !s.Game.IsSuggested)
            .Select(s => s.StartedAtUtc)
            .ToListAsync(cancellationToken);

        return
        [
            .. starts
                .Select(s => YearMonth.From(s.ToLocalTime()))
                .Distinct()
                .OrderDescending(),
        ];
    }

    public async Task<IReadOnlyList<MonthRecap>> GetMonthlyRecapsAsync(int maxGamesPerMonth, CancellationToken cancellationToken = default)
    {
        var (sessions, games) = await LoadLibrarySessionsAsync(range: null, cancellationToken);
        return MonthlyRecapBuilder.Build(sessions, games, TimeZoneInfo.Local, maxGamesPerMonth);
    }

    public async Task<MonthRecap?> GetMonthRecapAsync(YearMonth month, CancellationToken cancellationToken = default)
    {
        var range = ReportPeriod.ForMonth(month).ToUtcRange(TimeZoneInfo.Local);
        var (sessions, games) = await LoadLibrarySessionsAsync(range, cancellationToken);
        return MonthlyRecapBuilder.Build(sessions, games, TimeZoneInfo.Local).FirstOrDefault(r => r.Month == month);
    }

    /// <summary>Sessions of library games (optionally overlapping a range) plus the display data of their games.</summary>
    private async Task<(List<SessionSpan> Sessions, Dictionary<int, GameHeader> Games)> LoadLibrarySessionsAsync(
        DateRange? range, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Sessions.AsNoTracking().Where(s => !s.Game!.IsIgnored && !s.Game.IsSuggested);
        if (range is { } r)
            query = query.Where(s => s.StartedAtUtc < r.EndUtc && (s.EndedAtUtc >= r.StartUtc || s.IsOpen));

        var sessions = WithLiveEnd(await ToSpans(query).ToListAsync(cancellationToken));
        var gameIds = sessions.Select(s => s.GameId).Distinct().ToArray();
        var games = await db.Games
            .AsNoTracking()
            .Where(g => gameIds.Contains(g.Id))
            .Select(g => new GameHeader(g.Id, g.Name, g.CoverImagePath, g.BackgroundImagePath))
            .ToDictionaryAsync(g => g.Id, cancellationToken);

        return (sessions, games);
    }

    internal static GameInfo ToInfo(Game game) => new(
        game.Id, game.Name, game.Source, game.ExternalId, game.InstallDirectory, game.ExecutablePath,
        game.Summary, game.Genres, game.Developer, game.Publisher, game.ReleaseDate,
        game.CoverImagePath, game.BackgroundImagePath, game.IsIgnored, game.IsSuggested);

    private static IQueryable<SessionSpan> ToSpans(IQueryable<GameSession> query) =>
        query.Select(s => new SessionSpan(s.Id, s.GameId, s.StartedAtUtc, s.EndedAtUtc, s.IsOpen));

    /// <summary>An open session lasts until now, not just until its last heartbeat.</summary>
    private List<SessionSpan> WithLiveEnd(List<SessionSpan> sessions)
    {
        var now = time.GetUtcNow().UtcDateTime;
        for (var i = 0; i < sessions.Count; i++)
        {
            if (sessions[i].IsOpen && sessions[i].EndedAtUtc < now)
                sessions[i] = sessions[i] with { EndedAtUtc = now };
        }
        return sessions;
    }
}
