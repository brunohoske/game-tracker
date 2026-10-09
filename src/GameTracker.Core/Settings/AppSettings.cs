namespace GameTracker.Core.Settings;

public sealed record AppSettings
{
    public bool StartWithWindows { get; init; } = true;

    /// <summary>How often running processes are inspected.</summary>
    public int PollIntervalSeconds { get; init; } = 5;

    /// <summary>Sessions shorter than this are discarded (accidental launches, crashes on boot...).</summary>
    public int MinimumSessionSeconds { get; init; } = 60;

    /// <summary>Reopening a game within this gap continues the previous session (launcher restarts, quick crashes).</summary>
    public int SessionMergeGapSeconds { get; init; } = 120;

    /// <summary>Suggest unknown fullscreen 3D applications as games (behaviour-based detection).</summary>
    public bool SuggestGames { get; init; } = true;

    /// <summary>Extra folders where each sub-folder is a game (e.g. <c>D:\Jogos</c>).</summary>
    public IReadOnlyList<string> GameFolders { get; init; } = [];

    public string? IgdbClientId { get; init; }
    public string? IgdbClientSecret { get; init; }

    public bool HasIgdbCredentials =>
        !string.IsNullOrWhiteSpace(IgdbClientId) && !string.IsNullOrWhiteSpace(IgdbClientSecret);
}

public interface ISettingsStore
{
    AppSettings Current { get; }

    event EventHandler<AppSettings>? Changed;

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
