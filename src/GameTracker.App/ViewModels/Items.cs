using GameTracker.App.Formatting;
using GameTracker.Core.Library;
using GameTracker.Core.Reporting;

namespace GameTracker.App.ViewModels;

public sealed record NavigationItem(string Title, object Page)
{
    /// <summary>Also the accessible name of the tab (screen readers, UI automation).</summary>
    public override string ToString() => Title;
}

public sealed record PeriodOption(ReportPeriod Period, string Label, bool IsHeader = false)
{
    public static PeriodOption AllTime { get; } = new(ReportPeriod.AllTime, "Todo o período", IsHeader: true);

    public static PeriodOption Year(int year) => new(ReportPeriod.ForYear(year), $"{year} — ano inteiro", IsHeader: true);

    public static PeriodOption Month(YearMonth month) => new(ReportPeriod.ForMonth(month), Display.Month(month));

    public override string ToString() => Label;
}

public sealed record SortOption(string Label, Func<IEnumerable<GameCardViewModel>, IOrderedEnumerable<GameCardViewModel>> Order)
{
    public static IReadOnlyList<SortOption> All { get; } =
    [
        new("Jogados recentemente", games => games.OrderByDescending(g => g.IsPlaying).ThenByDescending(g => g.LastPlayedUtc)),
        new("Mais jogados", games => games.OrderByDescending(g => g.Playtime)),
        new("Mais sessões", games => games.OrderByDescending(g => g.SessionCount).ThenByDescending(g => g.Playtime)),
        new("Nome (A–Z)", games => games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)),
    ];

    public override string ToString() => Label;
}

public sealed class GameCardViewModel(GameSummary summary, bool isPlaying)
{
    public int GameId => summary.GameId;
    public string Name => summary.Name;
    public string Initials { get; } = Display.Initials(summary.Name);
    public string? CoverPath => summary.CoverImagePath;
    public TimeSpan Playtime => summary.Playtime;
    public int SessionCount => summary.SessionCount;
    public DateTime? LastPlayedUtc => summary.LastPlayedUtc;
    public bool IsPlaying { get; } = isPlaying;

    public string PlaytimeText => Display.Duration(summary.Playtime);
    public string SessionsText => Display.Count(summary.SessionCount, "sessão", "sessões");
    public string LastPlayedText => Display.RelativeDate(summary.LastPlayedUtc);
}

/// <summary>A game detected by behaviour, waiting for "Is this a game?".</summary>
public sealed class SuggestionViewModel(GameSuggestion suggestion)
{
    public int GameId => suggestion.Game.Id;
    public string Name => suggestion.Game.Name;
    public string? CoverPath => suggestion.Game.CoverImagePath;
    public string? ExecutablePath => suggestion.Game.ExecutablePath;
    public string PlaytimeText => Display.Duration(suggestion.Playtime) + " já registrados";
}

public sealed record StatItem(string Label, string Value, string Glyph);

public sealed record MonthBar(string Label, string Value, double Height, bool IsHighlighted);

public sealed record SessionRow(string Date, string TimeRange, string Duration, bool IsOpen);

/// <summary>Overlays that react to Esc / clicking outside.</summary>
public interface IOverlay
{
    void Cancel();
}
