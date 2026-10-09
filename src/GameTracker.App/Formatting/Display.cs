using System.Globalization;
using GameTracker.Core.Reporting;

namespace GameTracker.App.Formatting;

/// <summary>Human-friendly, pt-BR formatting of durations, dates and periods.</summary>
public static class Display
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");

    public static string Duration(TimeSpan duration)
    {
        if (duration < TimeSpan.FromMinutes(1))
            return duration <= TimeSpan.Zero ? "0min" : string.Create(Culture, $"{duration.Seconds}s");

        var hours = (long)duration.TotalHours;
        return hours > 0
            ? string.Create(Culture, $"{hours:N0}h {duration.Minutes:00}min")
            : string.Create(Culture, $"{duration.Minutes}min");
    }

    /// <summary>Compact variant for big numbers: "128h" instead of "128h 04min".</summary>
    public static string DurationShort(TimeSpan duration) =>
        duration.TotalHours >= 100 ? string.Create(Culture, $"{(long)duration.TotalHours:N0}h") : Duration(duration);

    public static string Count(int value, string singular, string plural) =>
        string.Create(Culture, $"{value:N0} {(value == 1 ? singular : plural)}");

    public static string RelativeDate(DateTime? utc)
    {
        if (utc is not { } value)
            return "Nunca";

        var local = value.ToLocalTime();
        var days = (DateTime.Today - local.Date).Days;
        return days switch
        {
            0 => "Hoje, " + local.ToString("HH:mm", Culture),
            1 => "Ontem, " + local.ToString("HH:mm", Culture),
            < 7 => string.Create(Culture, $"Há {days} dias"),
            _ => local.ToString("dd MMM yyyy", Culture),
        };
    }

    public static string Date(DateTime? utc) =>
        utc?.ToLocalTime().ToString("dd/MM/yyyy", Culture) ?? "—";

    public static string Month(YearMonth month) =>
        Capitalize(new DateTime(month.Year, month.Month, 1).ToString("MMMM 'de' yyyy", Culture));

    public static string MonthShort(YearMonth month) =>
        Capitalize(new DateTime(month.Year, month.Month, 1).ToString("MMM", Culture).TrimEnd('.'));

    public static string Period(ReportPeriod period) => period.Kind switch
    {
        PeriodKind.Year => string.Create(Culture, $"Ano de {period.Year}"),
        PeriodKind.Month => Month(new YearMonth(period.Year, period.Month)),
        _ => "Todo o período",
    };

    /// <summary>Up to two initials, shown on cards while a game has no cover.</summary>
    public static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpper(word[0], Culture)));

    public static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], Culture) + text[1..];
}
