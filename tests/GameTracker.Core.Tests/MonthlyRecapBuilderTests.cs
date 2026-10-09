using GameTracker.Core.Reporting;

namespace GameTracker.Core.Tests;

public class MonthlyRecapBuilderTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static readonly Dictionary<int, GameHeader> Games = new()
    {
        [1] = new GameHeader(1, "Hades", @"C:\hades-cover.jpg", @"C:\hades-bg.jpg"),
        [2] = new GameHeader(2, "Celeste", @"C:\celeste-cover.jpg", null),
        [3] = new GameHeader(3, "Balatro", null, null),
    };

    private static long _nextId;

    private static SessionSpan Session(int gameId, DateTime start, TimeSpan duration) =>
        new(++_nextId, gameId, DateTime.SpecifyKind(start, DateTimeKind.Utc), DateTime.SpecifyKind(start + duration, DateTimeKind.Utc), false);

    [Fact]
    public void Groups_by_month_most_recent_first_and_ranks_games()
    {
        var recaps = MonthlyRecapBuilder.Build(
        [
            Session(1, new DateTime(2026, 9, 3, 20, 0, 0), TimeSpan.FromHours(1)),
            Session(2, new DateTime(2026, 9, 4, 20, 0, 0), TimeSpan.FromHours(3)),
            Session(2, new DateTime(2026, 9, 4, 23, 30, 0), TimeSpan.FromMinutes(20)),
            Session(1, new DateTime(2026, 10, 1, 10, 0, 0), TimeSpan.FromHours(2)),
        ], Games, Utc);

        Assert.Equal([new YearMonth(2026, 10), new YearMonth(2026, 9)], recaps.Select(r => r.Month));

        var september = recaps[1];
        Assert.Equal(TimeSpan.FromMinutes(260), september.TotalPlaytime);
        Assert.Equal(3, september.SessionCount);
        Assert.Equal(2, september.GameCount);
        Assert.Equal(2, september.ActiveDays);
        Assert.Equal(TimeSpan.FromHours(3), september.LongestSession);
        Assert.Equal(["Celeste", "Hades"], september.Games.Select(g => g.Name));
        Assert.Equal(@"C:\celeste-cover.jpg", september.FeaturedBackgroundPath); // no background: falls back to cover
    }

    [Fact]
    public void Session_crossing_months_is_split()
    {
        var recaps = MonthlyRecapBuilder.Build(
            [Session(1, new DateTime(2026, 9, 30, 23, 0, 0), TimeSpan.FromHours(2))], Games, Utc);

        Assert.Equal(TimeSpan.FromHours(1), recaps.Single(r => r.Month == new YearMonth(2026, 10)).TotalPlaytime);
        Assert.Equal(TimeSpan.FromHours(1), recaps.Single(r => r.Month == new YearMonth(2026, 9)).TotalPlaytime);
    }

    [Fact]
    public void Truncates_games_but_keeps_totals()
    {
        var recaps = MonthlyRecapBuilder.Build(
        [
            Session(1, new DateTime(2026, 10, 1), TimeSpan.FromHours(3)),
            Session(2, new DateTime(2026, 10, 2), TimeSpan.FromHours(2)),
            Session(3, new DateTime(2026, 10, 3), TimeSpan.FromHours(1)),
        ], Games, Utc, maxGamesPerMonth: 2);

        var october = Assert.Single(recaps);
        Assert.True(october.IsTruncated);
        Assert.Equal(3, october.GameCount);
        Assert.Equal(TimeSpan.FromHours(6), october.TotalPlaytime);
        Assert.Equal(["Hades", "Celeste"], october.Games.Select(g => g.Name));
        Assert.Equal(@"C:\hades-bg.jpg", october.FeaturedBackgroundPath);
    }

    [Fact]
    public void Ignores_sessions_of_unknown_games() =>
        Assert.Empty(MonthlyRecapBuilder.Build([Session(99, new DateTime(2026, 10, 1), TimeSpan.FromHours(1))], Games, Utc));
}
