using GameTracker.Core.Reporting;

namespace GameTracker.Core.Tests;

public class PlaytimeCalculatorTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static SessionSpan Span(DateTime start, DateTime end) => new(1, 1, start, end, false);

    [Fact]
    public void Session_crossing_month_boundary_is_split()
    {
        var session = Span(new DateTime(2026, 9, 30, 23, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 1, 1, 30, 0, DateTimeKind.Utc));

        var september = ReportPeriod.ForMonth(new YearMonth(2026, 9)).ToUtcRange(Utc);
        var october = ReportPeriod.ForMonth(new YearMonth(2026, 10)).ToUtcRange(Utc);

        Assert.Equal(TimeSpan.FromHours(1), PlaytimeCalculator.Clip(session, september));
        Assert.Equal(TimeSpan.FromMinutes(90), PlaytimeCalculator.Clip(session, october));
        Assert.Equal(TimeSpan.FromMinutes(150), PlaytimeCalculator.Clip(session, null));
    }

    [Fact]
    public void Monthly_history_covers_requested_months_in_order()
    {
        var sessions = new[]
        {
            Span(new DateTime(2026, 8, 10, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc)),
            Span(new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 2, 10, 45, 0, DateTimeKind.Utc)),
        };

        var months = PlaytimeCalculator.ByMonth(sessions, new YearMonth(2026, 10), 3, Utc);

        Assert.Equal([new YearMonth(2026, 8), new YearMonth(2026, 9), new YearMonth(2026, 10)], months.Select(m => m.Month));
        Assert.Equal([TimeSpan.FromHours(2), TimeSpan.Zero, TimeSpan.FromMinutes(45)], months.Select(m => m.Playtime));
    }

    [Fact]
    public void Year_period_spans_whole_local_year()
    {
        var range = ReportPeriod.ForYear(2026).ToUtcRange(Utc)!.Value;

        Assert.Equal(new DateTime(2026, 1, 1), range.StartUtc);
        Assert.Equal(new DateTime(2027, 1, 1), range.EndUtc);
        Assert.Null(ReportPeriod.AllTime.ToUtcRange(Utc));
    }
}
