namespace GameTracker.Core.Reporting;

/// <summary>Display data of a game used by recaps.</summary>
public sealed record GameHeader(int Id, string Name, string? CoverImagePath, string? BackgroundImagePath);

/// <summary>Everything played in one calendar month (local time).</summary>
/// <param name="Games">Most played first; may be truncated (see <see cref="IsTruncated"/>).</param>
/// <param name="FeaturedBackgroundPath">Artwork of the month's most played game.</param>
public sealed record MonthRecap(
    YearMonth Month,
    TimeSpan TotalPlaytime,
    int SessionCount,
    int GameCount,
    int ActiveDays,
    TimeSpan LongestSession,
    IReadOnlyList<GameSummary> Games,
    string? FeaturedBackgroundPath)
{
    public bool IsTruncated => Games.Count < GameCount;
}

/// <summary>
/// Groups sessions into monthly recaps. A session crossing a month boundary contributes its overlapping part to
/// each month; it counts as a session (and an active day) in every month it touches.
/// </summary>
public static class MonthlyRecapBuilder
{
    public static IReadOnlyList<MonthRecap> Build(
        IEnumerable<SessionSpan> sessions,
        IReadOnlyDictionary<int, GameHeader> games,
        TimeZoneInfo timeZone,
        int? maxGamesPerMonth = null)
    {
        var months = new Dictionary<YearMonth, MonthAccumulator>();

        foreach (var session in sessions)
        {
            if (!games.ContainsKey(session.GameId))
                continue;

            var startLocal = TimeZoneInfo.ConvertTimeFromUtc(session.StartedAtUtc, timeZone);
            var endLocal = TimeZoneInfo.ConvertTimeFromUtc(session.EndedAtUtc, timeZone);
            var first = YearMonth.From(startLocal);
            var last = YearMonth.From(endLocal);

            for (var month = first; month.CompareTo(last) <= 0; month = month.AddMonths(1))
            {
                var range = ReportPeriod.ForMonth(month).ToUtcRange(timeZone)!.Value;
                var playtime = range.Overlap(session.StartedAtUtc, session.EndedAtUtc);
                if (playtime <= TimeSpan.Zero && month != first)
                    continue;

                var day = month == first ? DateOnly.FromDateTime(startLocal) : new DateOnly(month.Year, month.Month, 1);
                if (!months.TryGetValue(month, out var accumulator))
                    months[month] = accumulator = new MonthAccumulator();
                accumulator.Add(session, playtime, day);
            }
        }

        return
        [
            .. months
                .OrderByDescending(m => m.Key)
                .Select(m => m.Value.ToRecap(m.Key, games, maxGamesPerMonth)),
        ];
    }

    private sealed class MonthAccumulator
    {
        private readonly Dictionary<int, GameAccumulator> _games = [];
        private readonly HashSet<DateOnly> _days = [];
        private TimeSpan _total;
        private TimeSpan _longest;
        private int _sessions;

        public void Add(SessionSpan session, TimeSpan playtime, DateOnly day)
        {
            if (!_games.TryGetValue(session.GameId, out var game))
                _games[session.GameId] = game = new GameAccumulator();

            game.Playtime += playtime;
            game.Sessions++;
            if (session.EndedAtUtc > game.LastPlayedUtc)
                game.LastPlayedUtc = session.EndedAtUtc;

            _total += playtime;
            _sessions++;
            _days.Add(day);
            if (playtime > _longest)
                _longest = playtime;
        }

        public MonthRecap ToRecap(YearMonth month, IReadOnlyDictionary<int, GameHeader> headers, int? maxGames)
        {
            var ranked = _games
                .OrderByDescending(g => g.Value.Playtime)
                .ThenByDescending(g => g.Value.Sessions)
                .Take(maxGames ?? int.MaxValue)
                .Select(g =>
                {
                    var header = headers[g.Key];
                    return new GameSummary(header.Id, header.Name, header.CoverImagePath, g.Value.Playtime, g.Value.Sessions, g.Value.LastPlayedUtc);
                })
                .ToList();

            var featured = ranked.Count == 0 ? null : headers[ranked[0].GameId];
            return new MonthRecap(
                month,
                _total,
                _sessions,
                _games.Count,
                _days.Count,
                _longest,
                ranked,
                featured?.BackgroundImagePath ?? featured?.CoverImagePath);
        }
    }

    private sealed class GameAccumulator
    {
        public TimeSpan Playtime { get; set; }
        public int Sessions { get; set; }
        public DateTime LastPlayedUtc { get; set; }
    }
}
