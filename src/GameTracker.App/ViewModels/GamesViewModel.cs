using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTracker.App.Formatting;
using GameTracker.App.Services;
using GameTracker.Core.Library;
using GameTracker.Core.Metadata;
using GameTracker.Core.Reporting;
using GameTracker.Core.Tracking;

namespace GameTracker.App.ViewModels;

/// <summary>"Jogos" tab: library grid with period filter, sorting and search, plus the game detail page.</summary>
public sealed partial class GamesViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan LiveRefreshInterval = TimeSpan.FromMinutes(1);

    private readonly IReportService _reports;
    private readonly IGameLibrary _library;
    private readonly IActiveGamesMonitor _activeGames;
    private readonly IMetadataQueue _metadata;
    private readonly IDialogService _dialogs;
    private readonly Func<GameDetailViewModel> _detailFactory;
    private readonly DispatcherTimer _liveTimer;
    private readonly DispatcherTimer _debounceTimer;

    private IReadOnlyList<GameCardViewModel> _allGames = [];
    private CancellationTokenSource? _loadCancellation;
    private bool _isActivated;
    private bool _isDisposed;

    public GamesViewModel(
        IReportService reports,
        IGameLibrary library,
        IActiveGamesMonitor activeGames,
        IMetadataQueue metadata,
        IDialogService dialogs,
        Func<GameDetailViewModel> detailFactory)
    {
        _reports = reports;
        _library = library;
        _activeGames = activeGames;
        _metadata = metadata;
        _dialogs = dialogs;
        _detailFactory = detailFactory;

        _liveTimer = new DispatcherTimer { Interval = LiveRefreshInterval };
        _liveTimer.Tick += async (_, _) => await ReloadAsync();
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _debounceTimer.Tick += async (_, _) =>
        {
            _debounceTimer.Stop();
            await ReloadAsync();
        };

        _activeGames.ActiveGamesChanged += OnBackgroundChange;
        _metadata.GameUpdated += OnBackgroundChange;
    }

    public IReadOnlyList<SortOption> SortOptions => SortOption.All;

    [ObservableProperty]
    public partial IReadOnlyList<PeriodOption> Periods { get; private set; } = [PeriodOption.AllTime];

    [ObservableProperty]
    public partial PeriodOption? SelectedPeriod { get; set; } = PeriodOption.AllTime;

    [ObservableProperty]
    public partial SortOption? SelectedSort { get; set; } = SortOption.All[0];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<GameCardViewModel> Games { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<StatItem> Stats { get; private set; } = [];

    /// <summary>Games detected by behaviour awaiting the user's answer.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SuggestionViewModel> Suggestions { get; private set; } = [];

    [ObservableProperty]
    public partial string? NowPlaying { get; private set; }

    [ObservableProperty]
    public partial bool IsLibraryEmpty { get; private set; }

    [ObservableProperty]
    public partial bool HasNoResults { get; private set; }

    [ObservableProperty]
    public partial GameDetailViewModel? Detail { get; private set; }

    /// <summary>Called when the view is shown; loads data once.</summary>
    public async Task ActivateAsync()
    {
        if (_isActivated)
            return;
        _isActivated = true;
        await RefreshPeriodsAsync();
        await ReloadAsync();
    }

    /// <summary>The ComboBox briefly pushes null while its items are replaced: fall back to "all time".</summary>
    private ReportPeriod CurrentPeriod => SelectedPeriod?.Period ?? ReportPeriod.AllTime;

    partial void OnSelectedPeriodChanged(PeriodOption? value)
    {
        if (value is not null)
            _ = ReloadAsync();
    }

    partial void OnSelectedSortChanged(SortOption? value) => ApplyFilter();

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private Task OpenGameAsync(GameCardViewModel? card) => card is null ? Task.CompletedTask : OpenGameAsync(card.GameId);

    [RelayCommand]
    private async Task AddGameAsync()
    {
        if (_dialogs.PickExecutable() is not { } path)
            return;

        var gameId = await Task.Run(() => _library.AddManualGameAsync(path));
        await ReloadAsync();
        await OpenGameAsync(gameId);
    }

    [RelayCommand]
    private Task OpenSuggestionAsync(SuggestionViewModel? suggestion) =>
        suggestion is null ? Task.CompletedTask : OpenGameAsync(suggestion.GameId);

    [RelayCommand]
    private async Task ConfirmSuggestionAsync(SuggestionViewModel suggestion)
    {
        await Task.Run(() => _library.ConfirmSuggestionAsync(suggestion.GameId));
        await OnLibraryChangedAsync();
    }

    /// <summary>"Not a game": ignored for good (reversible in Settings), no confirmation needed.</summary>
    [RelayCommand]
    private async Task RejectSuggestionAsync(SuggestionViewModel suggestion)
    {
        await Task.Run(() => _library.SetIgnoredAsync(suggestion.GameId, ignored: true));
        await ReloadAsync();
    }

    private async Task OpenGameAsync(int gameId)
    {
        var detail = _detailFactory();
        detail.Initialize(gameId, CloseDetail, OnLibraryChangedAsync);
        Detail = detail;
        await detail.LoadAsync(CurrentPeriod);
    }

    private void CloseDetail() => Detail = null;

    private async Task OnLibraryChangedAsync()
    {
        await RefreshPeriodsAsync();
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        if (_isDisposed)
            return;

        // Plain CTS without timers/links need no disposal; cancelling supersedes an in-flight load.
        _loadCancellation?.Cancel();
        _loadCancellation = new CancellationTokenSource();
        var cancellation = _loadCancellation.Token;
        var period = CurrentPeriod;

        LibraryReport report;
        IReadOnlyList<GameSuggestion> suggestions;
        try
        {
            report = await Task.Run(() => _reports.GetLibraryReportAsync(period, cancellation), cancellation);
            suggestions = await Task.Run(() => _library.GetSuggestionsAsync(cancellation), cancellation);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var active = _activeGames.ActiveGames;
        var playing = active.Select(a => a.GameId).ToHashSet();
        _allGames = [.. report.Games.Select(g => new GameCardViewModel(g, playing.Contains(g.GameId)))];

        Suggestions = [.. suggestions.Select(s => new SuggestionViewModel(s))];
        NowPlaying = active.Count == 0 ? null : string.Join(", ", active.Select(a => a.Name));
        IsLibraryEmpty = _allGames.Count == 0;
        Stats =
        [
            new("Tempo jogado", Display.DurationShort(report.TotalPlaytime), ""),
            new("Sessões", report.SessionCount.ToString("N0", Display.Culture), ""),
            new("Jogos", report.Games.Count(g => g.SessionCount > 0).ToString("N0", Display.Culture), ""),
            new("Média por sessão", Display.Duration(report.AverageSession), ""),
        ];
        ApplyFilter();

        if (active.Count > 0)
            _liveTimer.Start();
        else
            _liveTimer.Stop();

        if (Detail is not null)
            await Detail.LoadAsync(period);
    }

    private async Task RefreshPeriodsAsync()
    {
        var months = await Task.Run(() => _reports.GetActiveMonthsAsync());
        var current = YearMonth.From(DateTime.Now);

        var options = new List<PeriodOption> { PeriodOption.AllTime };
        foreach (var year in months.Append(current).Distinct().GroupBy(m => m.Year).OrderByDescending(g => g.Key))
        {
            options.Add(PeriodOption.Year(year.Key));
            options.AddRange(year.OrderDescending().Select(PeriodOption.Month));
        }

        var selected = CurrentPeriod;
        Periods = options;
        // Keep the selection (records compare by value) so the ComboBox does not reset.
        SelectedPeriod = options.FirstOrDefault(o => o.Period == selected) ?? PeriodOption.AllTime;
    }

    private void ApplyFilter()
    {
        var search = SearchText.Trim();
        var filtered = search.Length == 0
            ? _allGames
            : _allGames.Where(g => g.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase));

        Games = [.. (SelectedSort ?? SortOption.All[0]).Order(filtered)];
        HasNoResults = !IsLibraryEmpty && Games.Count == 0;
    }

    /// <summary>Tracker/metadata events arrive on background threads and in bursts: debounce on the UI thread.</summary>
    private void OnBackgroundChange(object? sender, EventArgs e) =>
        _debounceTimer.Dispatcher.BeginInvoke(() =>
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        });

    private void OnBackgroundChange(object? sender, int gameId) => OnBackgroundChange(sender, EventArgs.Empty);

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        _activeGames.ActiveGamesChanged -= OnBackgroundChange;
        _metadata.GameUpdated -= OnBackgroundChange;
        _liveTimer.Stop();
        _debounceTimer.Stop();
        _loadCancellation?.Cancel();
    }
}
