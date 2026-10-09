namespace GameTracker.Core.Reporting;

public enum PeriodKind
{
    AllTime,
    Year,
    Month,
}

/// <summary>Half-open UTC interval [Start, End).</summary>
public readonly record struct DateRange(DateTime StartUtc, DateTime EndUtc)
{
    public TimeSpan Overlap(DateTime startUtc, DateTime endUtc)
    {
        var start = startUtc > StartUtc ? startUtc : StartUtc;
        var end = endUtc < EndUtc ? endUtc : EndUtc;
        return end > start ? end - start : TimeSpan.Zero;
    }
}

public readonly record struct YearMonth(int Year, int Month) : IComparable<YearMonth>
{
    public static YearMonth From(DateTime date) => new(date.Year, date.Month);

    public YearMonth AddMonths(int months)
    {
        var date = new DateTime(Year, Month, 1).AddMonths(months);
        return new YearMonth(date.Year, date.Month);
    }

    public int CompareTo(YearMonth other) => (Year * 12 + Month).CompareTo(other.Year * 12 + other.Month);
}

/// <summary>A reporting window expressed in the user's calendar (local time).</summary>
public sealed record ReportPeriod(PeriodKind Kind, int Year = 0, int Month = 0)
{
    public static ReportPeriod AllTime { get; } = new(PeriodKind.AllTime);

    public static ReportPeriod ForYear(int year) => new(PeriodKind.Year, year);

    public static ReportPeriod ForMonth(YearMonth month) => new(PeriodKind.Month, month.Year, month.Month);

    /// <summary>The UTC interval of this period, or null for all time.</summary>
    public DateRange? ToUtcRange(TimeZoneInfo timeZone)
    {
        (DateTime start, DateTime end) = Kind switch
        {
            PeriodKind.Year => (new DateTime(Year, 1, 1), new DateTime(Year + 1, 1, 1)),
            PeriodKind.Month => (new DateTime(Year, Month, 1), new DateTime(Year, Month, 1).AddMonths(1)),
            _ => (default, default),
        };

        return Kind == PeriodKind.AllTime
            ? null
            : new DateRange(ToUtc(start, timeZone), ToUtc(end, timeZone));
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone)
    {
        // Midnight can fall inside a daylight-saving gap (e.g. old Brazilian DST rules).
        while (timeZone.IsInvalidTime(local))
            local = local.AddMinutes(30);
        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }
}
