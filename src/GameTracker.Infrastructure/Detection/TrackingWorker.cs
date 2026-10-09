using GameTracker.Core.Detection;
using GameTracker.Core.Settings;
using GameTracker.Core.Tracking;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameTracker.Infrastructure.Detection;

/// <summary>
/// The always-on loop: every poll it takes a process snapshot, recognises games (rules first, then behaviour
/// for the foreground app) and feeds the <see cref="SessionTracker"/>. Detection results are cached per
/// process, so steady state is almost free.
/// </summary>
internal sealed class TrackingWorker(
    IProcessSnapshotProvider processes,
    IEnumerable<ILibraryScanner> scanners,
    GameDetector detector,
    BehaviorGameDetector behaviorDetector,
    SessionTracker tracker,
    ITrackingStore store,
    ISettingsStore settings,
    TimeProvider time,
    ILogger<TrackingWorker> logger) : BackgroundService, ILibraryChangeNotifier
{
    private static readonly TimeSpan LibraryRescanInterval = TimeSpan.FromMinutes(30);

    private readonly Dictionary<int, DetectedGame?> _matchCache = [];
    private readonly List<DetectedGame> _detected = [];
    private DateTimeOffset _nextLibraryScan = DateTimeOffset.MinValue;
    private int _rulesChanged = 1;
    private PeriodicTimer? _timer;

    public void NotifyLibraryChanged() => Interlocked.Exchange(ref _rulesChanged, 1);

    // Task.Run keeps the loop off the caller's synchronization context (the WPF dispatcher).
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() => RunAsync(stoppingToken), stoppingToken);

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        await tracker.InitializeAsync(stoppingToken);

        using var timer = _timer = new PeriodicTimer(TrackingOptions.From(settings.Current).PollInterval, time);
        settings.Changed += OnSettingsChanged;
        try
        {
            do
            {
                try
                {
                    await TickAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Tracking tick failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            settings.Changed -= OnSettingsChanged;
            await tracker.StopAsync(CancellationToken.None);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        await RefreshRulesIfNeededAsync(cancellationToken);

        var snapshot = processes.GetRunningProcesses();
        _detected.Clear();
        foreach (var process in snapshot)
        {
            if (!_matchCache.TryGetValue(process.ProcessId, out var match))
                _matchCache[process.ProcessId] = match = detector.Match(process);
            if (match is not null)
                _detected.Add(match);
        }

        if (settings.Current.SuggestGames)
            DetectByBehavior(snapshot);

        if (_matchCache.Count > snapshot.Count)
            PruneMatchCache(snapshot);
        behaviorDetector.Prune(snapshot);

        await tracker.ProcessAsync(_detected, cancellationToken);
    }

    private void DetectByBehavior(IReadOnlyList<RunningProcess> snapshot)
    {
        if (!behaviorDetector.TryDetect(snapshot, id => _matchCache.GetValueOrDefault(id) is not null, out var processId, out var suggestion))
            return;

        // From now on it is a known game: this process keeps its session even when not in the foreground,
        // and the executable is recognised directly on the next launch.
        logger.LogInformation("Suggested game by behaviour: {Name} ({Path})", suggestion.Name, suggestion.ExecutablePath);
        detector.Remember(suggestion);
        _matchCache[processId] = suggestion;
        _detected.Add(suggestion);
    }

    private async Task RefreshRulesIfNeededAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var rescan = now >= _nextLibraryScan;
        if (Interlocked.Exchange(ref _rulesChanged, 0) == 0 && !rescan)
            return;

        if (rescan)
        {
            detector.UpdateLibrary(ScanLibraries());
            _nextLibraryScan = now + LibraryRescanInterval;
        }

        detector.UpdateUserFolders(settings.Current.GameFolders);
        detector.UpdateKnownGames(await store.GetKnownGamesAsync(cancellationToken));
        tracker.ResetCache();
        _matchCache.Clear();
    }

    private List<LibraryEntry> ScanLibraries()
    {
        var entries = new List<LibraryEntry>();
        foreach (var scanner in scanners)
        {
            try
            {
                var found = scanner.Scan();
                entries.AddRange(found);
                logger.LogDebug("{Launcher}: {Count} installed games", scanner.Name, found.Count);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Text.Json.JsonException)
            {
                logger.LogWarning(ex, "Could not scan {Launcher} library", scanner.Name);
            }
        }
        return entries;
    }

    private void PruneMatchCache(IReadOnlyList<RunningProcess> snapshot)
    {
        var alive = snapshot.Select(p => p.ProcessId).ToHashSet();
        foreach (var id in _matchCache.Keys.Where(id => !alive.Contains(id)).ToArray())
            _matchCache.Remove(id);
    }

    private void OnSettingsChanged(object? sender, AppSettings newSettings)
    {
        if (_timer is { } timer)
            timer.Period = TrackingOptions.From(newSettings).PollInterval;
        NotifyLibraryChanged();
    }
}
