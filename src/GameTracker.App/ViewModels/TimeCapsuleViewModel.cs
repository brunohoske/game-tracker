using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTracker.App.Formatting;
using GameTracker.Core.Reporting;

namespace GameTracker.App.ViewModels;

/// <summary>
/// "Cápsula do tempo" tab: a timeline of every month played. Each month shows only its top games;
/// the full ranking is loaded on demand ("Ver {mês} completo") so months with dozens of games stay light.
/// </summary>
public sealed partial class TimeCapsuleViewModel(IReportService reports) : ObservableObject
{
    private const int GamesPerMonthPreview = 5;

    [ObservableProperty]
    public partial IReadOnlyList<object> Timeline { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    public partial MonthDetailViewModel? Detail { get; private set; }

    /// <summary>Called every time the tab is shown: cheap, and always reflects the latest sessions.</summary>
    public async Task ActivateAsync()
    {
        var recaps = await Task.Run(() => reports.GetMonthlyRecapsAsync(GamesPerMonthPreview));
        var record = recaps.Count == 0 ? TimeSpan.Zero : recaps.Max(r => r.TotalPlaytime);
        var currentMonth = YearMonth.From(DateTime.Now);

        var timeline = new List<object>();
        foreach (var year in recaps.GroupBy(r => r.Month.Year))
        {
            timeline.Add(new YearHeaderViewModel(year.Key, year.Aggregate(TimeSpan.Zero, (sum, r) => sum + r.TotalPlaytime), year.Count()));
            timeline.AddRange(year.Select(recap => new MonthRecapViewModel(
                recap,
                previous: recaps.FirstOrDefault(r => r.Month == recap.Month.AddMonths(-1)),
                isRecord: recaps.Count > 1 && recap.TotalPlaytime == record,
                isCurrent: recap.Month == currentMonth)));
        }

        Timeline = timeline;
        IsEmpty = recaps.Count == 0;

        if (Detail is not null)
            await OpenMonthAsync(Detail.Month);
    }

    [RelayCommand]
    private Task OpenMonth(MonthRecapViewModel month) => OpenMonthAsync(month.Month);

    private async Task OpenMonthAsync(YearMonth month)
    {
        var recap = await Task.Run(() => reports.GetMonthRecapAsync(month));
        Detail = recap is null ? null : new MonthDetailViewModel(recap, back: () => Detail = null);
    }
}

public sealed record YearHeaderViewModel(int Year, TimeSpan Total, int Months)
{
    public string Title => Year.ToString(Display.Culture);
    public string Summary => $"{Display.DurationShort(Total)} jogadas  ·  {Display.Count(Months, "mês", "meses")} com jogos";
}

/// <summary>Compact card of one month in the timeline.</summary>
public sealed class MonthRecapViewModel
{
    public MonthRecapViewModel(MonthRecap recap, MonthRecap? previous, bool isRecord, bool isCurrent)
    {
        Month = recap.Month;
        MonthName = Display.Capitalize(new DateTime(recap.Month.Year, recap.Month.Month, 1).ToString("MMMM", Display.Culture));
        TotalText = Display.DurationShort(recap.TotalPlaytime);
        Summary = string.Join("  ·  ",
            Display.Count(recap.SessionCount, "sessão", "sessões"),
            Display.Count(recap.GameCount, "jogo", "jogos"),
            Display.Count(recap.ActiveDays, "dia ativo", "dias ativos"));
        BackgroundPath = recap.FeaturedBackgroundPath;
        IsRecord = isRecord;
        IsCurrent = isCurrent;
        TopGames = [.. recap.Games.Select((game, index) => new RecapGameViewModel(game, isFeatured: index == 0))];
        MoreCount = recap.GameCount - recap.Games.Count;
        ViewFullText = $"Ver {MonthName.ToLower(Display.Culture)} completo";

        // A month still in progress is not compared: it would always look like a drop.
        if (!isCurrent && previous is { TotalPlaytime.Ticks: > 0 })
        {
            var change = (recap.TotalPlaytime - previous.TotalPlaytime) / previous.TotalPlaytime;
            var previousName = new DateTime(previous.Month.Year, previous.Month.Month, 1).ToString("MMMM", Display.Culture);
            IsTrendUp = change >= 0;
            TrendText = string.Create(Display.Culture, $"{(IsTrendUp ? "▲" : "▼")} {Math.Abs(change):P0} vs {previousName}");
        }
    }

    public YearMonth Month { get; }
    public string MonthName { get; }
    public string TotalText { get; }
    public string Summary { get; }
    public string? BackgroundPath { get; }
    public bool IsRecord { get; }
    public bool IsCurrent { get; }
    public IReadOnlyList<RecapGameViewModel> TopGames { get; }
    public int MoreCount { get; }
    public string MoreText => $"+{MoreCount}";
    public string ViewFullText { get; }
    public string? TrendText { get; }
    public bool IsTrendUp { get; }
}

public sealed class RecapGameViewModel(GameSummary game, bool isFeatured)
{
    public string Name => game.Name;
    public string Initials { get; } = Display.Initials(game.Name);
    public string? CoverPath => game.CoverImagePath;
    public string PlaytimeText => Display.Duration(game.Playtime);
    public bool IsFeatured { get; } = isFeatured;
}

/// <summary>"Ver {mês} completo": full ranking of a month.</summary>
public sealed class MonthDetailViewModel
{
    public MonthDetailViewModel(MonthRecap recap, Action back)
    {
        BackCommand = new RelayCommand(back);
        Month = recap.Month;
        Title = Display.Month(recap.Month);
        Stats =
        [
            new("Tempo total", Display.Duration(recap.TotalPlaytime), ""),
            new("Sessões", recap.SessionCount.ToString("N0", Display.Culture), ""),
            new("Jogos", recap.GameCount.ToString("N0", Display.Culture), ""),
            new("Dias ativos", recap.ActiveDays.ToString("N0", Display.Culture), ""),
            new("Sessão mais longa", Display.Duration(recap.LongestSession), ""),
            new("Média por dia ativo", Display.Duration(recap.ActiveDays == 0 ? TimeSpan.Zero : recap.TotalPlaytime / recap.ActiveDays), ""),
        ];

        var top = recap.Games.Count == 0 ? TimeSpan.Zero : recap.Games[0].Playtime;
        Games = [.. recap.Games.Select((game, index) => new RankedGameViewModel(index + 1, game, top, recap.TotalPlaytime))];
    }

    public IRelayCommand BackCommand { get; }
    public YearMonth Month { get; }
    public string Title { get; }
    public IReadOnlyList<StatItem> Stats { get; }
    public IReadOnlyList<RankedGameViewModel> Games { get; }
}

public sealed class RankedGameViewModel(int rank, GameSummary game, TimeSpan top, TimeSpan monthTotal)
{
    public int Rank { get; } = rank;
    public string RankText => $"#{Rank}";
    public string Name => game.Name;
    public string Initials { get; } = Display.Initials(game.Name);
    public string? CoverPath => game.CoverImagePath;
    public string PlaytimeText => Display.Duration(game.Playtime);
    public string SessionsText => Display.Count(game.SessionCount, "sessão", "sessões");
    public string ShareText => monthTotal <= TimeSpan.Zero ? "—" : (game.Playtime / monthTotal).ToString("P0", Display.Culture) + " do mês";

    /// <summary>Bar length relative to the month's most played game (0..1).</summary>
    public double BarRatio => top <= TimeSpan.Zero ? 0 : Math.Max(0.02, game.Playtime / top);

    public bool IsPodium => Rank <= 3;
}
