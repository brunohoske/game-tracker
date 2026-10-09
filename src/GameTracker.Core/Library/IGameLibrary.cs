using GameTracker.Core.Reporting;

namespace GameTracker.Core.Library;

/// <summary>User-driven changes to the game library.</summary>
public interface IGameLibrary
{
    /// <summary>Registers an executable the automatic detection does not recognise.</summary>
    Task<int> AddManualGameAsync(string executablePath, CancellationToken cancellationToken = default);

    Task RenameAsync(int gameId, string name, CancellationToken cancellationToken = default);

    /// <summary>"Not a game": stops tracking it and hides it from reports.</summary>
    Task SetIgnoredAsync(int gameId, bool ignored, CancellationToken cancellationToken = default);

    Task DeleteAsync(int gameId, CancellationToken cancellationToken = default);

    void RefreshMetadata(int gameId);

    Task<IReadOnlyList<GameInfo>> GetIgnoredGamesAsync(CancellationToken cancellationToken = default);

    /// <summary>Games detected by behaviour awaiting confirmation, newest first, with the time already recorded.</summary>
    Task<IReadOnlyList<GameSuggestion>> GetSuggestionsAsync(CancellationToken cancellationToken = default);

    /// <summary>"Yes, it is a game": moves a suggestion into the library (its recorded sessions are kept).</summary>
    Task ConfirmSuggestionAsync(int gameId, CancellationToken cancellationToken = default);
}

public sealed record GameSuggestion(GameInfo Game, TimeSpan Playtime);
