namespace GameTracker.Core.Domain;

public sealed class Game
{
    public int Id { get; set; }

    /// <summary>Stable identity used to recognise the game across runs. See <see cref="GameKeys"/>.</summary>
    public required string Key { get; set; }

    public required string Name { get; set; }
    public GameSource Source { get; set; }

    /// <summary>Identifier in the source store (Steam app id, Epic app name, GOG id...).</summary>
    public string? ExternalId { get; set; }

    public string? InstallDirectory { get; set; }
    public string? ExecutablePath { get; set; }

    /// <summary>User flagged it as "not a game": it is still recognised, but never tracked or listed.</summary>
    public bool IsIgnored { get; set; }

    /// <summary>
    /// Detected by behaviour and awaiting the user's confirmation. Its sessions are already recorded,
    /// but it stays out of the library and reports until confirmed.
    /// </summary>
    public bool IsSuggested { get; set; }

    public DateTime AddedAtUtc { get; set; }

    // Metadata (filled asynchronously by the metadata providers).
    public string? Summary { get; set; }
    public List<string> Genres { get; set; } = [];
    public string? Developer { get; set; }
    public string? Publisher { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? CoverImagePath { get; set; }
    public string? BackgroundImagePath { get; set; }
    public long? IgdbId { get; set; }
    public DateTime? MetadataUpdatedAtUtc { get; set; }

    public List<GameSession> Sessions { get; set; } = [];
}
