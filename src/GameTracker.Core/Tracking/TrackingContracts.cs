using GameTracker.Core.Detection;
using GameTracker.Core.Settings;

namespace GameTracker.Core.Tracking;

public sealed record TrackedGame(int GameId, bool IsIgnored, bool IsNew);

public enum SessionEndResult
{
    Saved,

    /// <summary>Shorter than the minimum duration.</summary>
    Discarded,

    /// <summary>Discarded and the game had no other session, so it was removed as well.</summary>
    DiscardedWithGame,
}

/// <summary>Persistence used by <see cref="SessionTracker"/>.</summary>
public interface ITrackingStore
{
    /// <summary>Closes sessions left open by an abnormal shutdown (they keep their last heartbeat as end).</summary>
    Task CloseOrphanedSessionsAsync(CancellationToken cancellationToken);

    Task<TrackedGame> GetOrCreateGameAsync(DetectedGame game, CancellationToken cancellationToken);

    /// <summary>Starts a session, or reopens the last one when it ended less than <paramref name="mergeGap"/> ago.</summary>
    Task<long> StartSessionAsync(int gameId, DateTime nowUtc, TimeSpan mergeGap, CancellationToken cancellationToken);

    /// <summary>Heartbeat: moves the end of open sessions forward.</summary>
    Task TouchSessionsAsync(IReadOnlyCollection<long> sessionIds, DateTime nowUtc, CancellationToken cancellationToken);

    Task<SessionEndResult> EndSessionAsync(long sessionId, DateTime endedAtUtc, TimeSpan minimumDuration, CancellationToken cancellationToken);

    /// <summary>Games with a known executable, used to restore detection rules.</summary>
    Task<IReadOnlyList<DetectedGame>> GetKnownGamesAsync(CancellationToken cancellationToken);
}

/// <summary>Lets the UI tell the tracking loop that games or rules changed (ignored, added, removed...).</summary>
public interface ILibraryChangeNotifier
{
    void NotifyLibraryChanged();
}

/// <summary>Live view of what is being played right now.</summary>
public interface IActiveGamesMonitor
{
    IReadOnlyCollection<ActiveGame> ActiveGames { get; }

    event EventHandler? ActiveGamesChanged;

    /// <summary>A game detected by behaviour was seen for the first time and awaits confirmation (background thread).</summary>
    event EventHandler<ActiveGame>? GameSuggested;
}

public sealed record ActiveGame(int GameId, string Name, DateTime StartedAtUtc);

public sealed record TrackingOptions(
    TimeSpan PollInterval,
    TimeSpan MinimumSessionDuration,
    TimeSpan MergeGap,
    TimeSpan HeartbeatInterval)
{
    public static readonly TimeSpan DefaultHeartbeat = TimeSpan.FromMinutes(1);

    public static TrackingOptions From(AppSettings settings) => new(
        TimeSpan.FromSeconds(Math.Clamp(settings.PollIntervalSeconds, 1, 60)),
        TimeSpan.FromSeconds(Math.Max(0, settings.MinimumSessionSeconds)),
        TimeSpan.FromSeconds(Math.Max(0, settings.SessionMergeGapSeconds)),
        DefaultHeartbeat);
}
