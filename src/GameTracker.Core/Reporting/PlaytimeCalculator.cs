namespace GameTracker.Core.Reporting;

/// <summary>Pure playtime arithmetic. Sessions crossing a period boundary only count their overlapping part.</summary>
public static class PlaytimeCalculator
{
    public static TimeSpan Clip(SessionSpan session, DateRange? range) =>
        range?.Overlap(session.StartedAtUtc, session.EndedAtUtc) ?? session.Duration;

    public static bool Overlaps(SessionSpan session, DateRange? range) =>
        range is not { } r || (session.StartedAtUtc < r.EndUtc && session.EndedAtUtc >= r.StartUtc);

    public static TimeSpan Total(IEnumerable<SessionSpan> sessions, DateRange? range)
    {
        var total = TimeSpan.Zero;
        foreach (var session in sessions)
            total += Clip(session, range);
        return total;
    }

    /// <summary>Playtime per local month for the <paramref name="count"/> months ending at <paramref name="last"/>.</summary>
    public static IReadOnlyList<MonthlyPlaytime> ByMonth(
        IReadOnlyCollection<SessionSpan> sessions, YearMonth last, int count, TimeZoneInfo timeZone)
    {
        var result = new MonthlyPlaytime[count];
        for (var i = 0; i < count; i++)
        {
            var month = last.AddMonths(i - count + 1);
            var range = ReportPeriod.ForMonth(month).ToUtcRange(timeZone);
            result[i] = new MonthlyPlaytime(month, Total(sessions, range));
        }
        return result;
    }
}
