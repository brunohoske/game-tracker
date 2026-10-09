using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTracker.App.Formatting;
using GameTracker.App.Services;
using GameTracker.Core.Domain;
using GameTracker.Core.Library;
using GameTracker.Core.Reporting;
using GameTracker.Core.Tracking;

namespace GameTracker.App.ViewModels;

/// <summary>Game page: artwork, details, period statistics, 12-month chart and session history.</summary>
public sealed partial class GameDetailViewModel(
    IReportService reports,
    IGameLibrary library,
    IActiveGamesMonitor activeGames,
    IDialogService dialogs) : ObservableObject
{
    private const int MaxSessionsShown = 150;
    private const double ChartHeight = 120;

    private int _gameId;
    private Action _close = () => { };
    private Func<Task> _libraryChanged = () => Task.CompletedTask;
    private ReportPeriod _period = ReportPeriod.AllTime;

    [ObservableProperty]
    public partial GameInfo? Game { get; private set; }

    [ObservableProperty]
    public partial bool IsPlaying { get; private set; }

    [ObservableProperty]
    public partial string PeriodLabel { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string MetaLine { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<StatItem> Stats { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<MonthBar> Months { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<SessionRow> Sessions { get; private set; } = [];

    [ObservableProperty]
    public partial string SessionsCaption { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsEditingName { get; set; }

    [ObservableProperty]
    public partial string EditableName { get; set; } = string.Empty;

    public bool CanOpenFolder => Game?.InstallDirectory is { } dir && Directory.Exists(dir);

    public void Initialize(int gameId, Action close, Func<Task> libraryChanged)
    {
        _gameId = gameId;
        _close = close;
        _libraryChanged = libraryChanged;
    }

    public async Task LoadAsync(ReportPeriod period)
    {
        _period = period;
        var report = await Task.Run(() => reports.GetGameReportAsync(_gameId, period));
        if (report is null)
        {
            _close();
            return;
        }

        Game = report.Game;
        IsPlaying = activeGames.ActiveGames.Any(a => a.GameId == _gameId);
        PeriodLabel = Display.Period(period);
        MetaLine = BuildMetaLine(report.Game);
        OnPropertyChanged(nameof(CanOpenFolder));

        Stats =
        [
            new("Tempo no período", Display.Duration(report.Playtime), ""),
            new("Sessões", report.SessionCount.ToString("N0", Display.Culture), ""),
            new("Média por sessão", Display.Duration(report.AverageSession), ""),
            new("Sessão mais longa", Display.Duration(report.LongestSession), ""),
            new("Tempo total", Display.Duration(report.AllTimePlaytime), ""),
            new("Última vez", Display.RelativeDate(report.LastPlayedUtc), ""),
        ];

        Months = BuildMonths(report.Monthly, period);
        Sessions = [.. report.Sessions.Take(MaxSessionsShown).Select(ToRow)];
        SessionsCaption = report.Sessions.Count > MaxSessionsShown
            ? string.Create(Display.Culture, $"Mostrando as {MaxSessionsShown} mais recentes de {report.Sessions.Count:N0}")
            : Display.Count(report.Sessions.Count, "sessão", "sessões");
    }

    [RelayCommand]
    private void Back() => _close();

    [RelayCommand]
    private void BeginRename()
    {
        EditableName = Game?.Name ?? string.Empty;
        IsEditingName = true;
    }

    [RelayCommand]
    private void CancelRename() => IsEditingName = false;

    [RelayCommand]
    private async Task CommitRenameAsync()
    {
        IsEditingName = false;
        if (Game is null || string.IsNullOrWhiteSpace(EditableName) || EditableName.Trim() == Game.Name)
            return;

        await Task.Run(() => library.RenameAsync(_gameId, EditableName));
        await _libraryChanged();
        await LoadAsync(_period);
    }

    [RelayCommand]
    private void RefreshMetadata() => library.RefreshMetadata(_gameId);

    [RelayCommand]
    private void OpenFolder()
    {
        if (CanOpenFolder)
            Process.Start(new ProcessStartInfo { FileName = Game!.InstallDirectory, UseShellExecute = true });
    }

    [RelayCommand]
    private async Task IgnoreAsync()
    {
        var confirmed = await dialogs.ConfirmAsync(
            "Não é um jogo?",
            $"\"{Game?.Name}\" deixará de ser rastreado e sairá da biblioteca e dos relatórios. Você pode restaurá-lo em Configurações.",
            "Ignorar");
        if (!confirmed)
            return;

        await Task.Run(() => library.SetIgnoredAsync(_gameId, true));
        _close();
        await _libraryChanged();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var confirmed = await dialogs.ConfirmAsync(
            "Remover jogo",
            $"Remover \"{Game?.Name}\" e todo o histórico de sessões? Esta ação não pode ser desfeita. Se o jogo for aberto novamente, ele voltará a ser registrado.",
            "Remover",
            destructive: true);
        if (!confirmed)
            return;

        await Task.Run(() => library.DeleteAsync(_gameId));
        _close();
        await _libraryChanged();
    }

    private static string BuildMetaLine(GameInfo game)
    {
        var parts = new List<string>();
        if (game.Developer is { } developer)
            parts.Add(developer);
        if (game.Publisher is { } publisher && publisher != game.Developer)
            parts.Add(publisher);
        if (game.ReleaseDate is { } release)
            parts.Add(release.Year.ToString(Display.Culture));
        parts.Add(game.Source switch
        {
            GameSource.Steam => "Steam",
            GameSource.Epic => "Epic Games",
            GameSource.Gog => "GOG",
            GameSource.Ubisoft => "Ubisoft Connect",
            GameSource.Manual => "Adicionado manualmente",
            _ => "Detectado automaticamente",
        });
        return string.Join("  •  ", parts);
    }

    private static IReadOnlyList<MonthBar> BuildMonths(IReadOnlyList<MonthlyPlaytime> monthly, ReportPeriod period)
    {
        var max = monthly.Count == 0 ? TimeSpan.Zero : monthly.Max(m => m.Playtime);
        return
        [
            .. monthly.Select(m => new MonthBar(
                Display.MonthShort(m.Month),
                Display.Month(m.Month) + ": " + Display.Duration(m.Playtime),
                max <= TimeSpan.Zero ? 2 : Math.Max(2, ChartHeight * (m.Playtime / max)),
                period.Kind switch
                {
                    PeriodKind.Month => m.Month == new YearMonth(period.Year, period.Month),
                    PeriodKind.Year => m.Month.Year == period.Year,
                    _ => true,
                })),
        ];
    }

    private static SessionRow ToRow(SessionSpan session)
    {
        var start = session.StartedAtUtc.ToLocalTime();
        var end = session.EndedAtUtc.ToLocalTime();
        return new SessionRow(
            start.ToString("dd/MM/yyyy", Display.Culture) + "  ·  " + start.ToString("dddd", Display.Culture),
            session.IsOpen ? $"{start:HH:mm} – agora" : $"{start:HH:mm} – {end:HH:mm}",
            Display.Duration(session.Duration),
            session.IsOpen);
    }
}
