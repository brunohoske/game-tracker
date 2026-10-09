using GameTracker.Core.Domain;

namespace GameTracker.Core.Metadata;

public sealed record MetadataQuery(string Name, GameSource Source, string? ExternalId)
{
    public string? SteamAppId => Source == GameSource.Steam ? ExternalId : null;
}

/// <summary>Information about a game gathered from an external catalog.</summary>
public sealed record GameMetadata
{
    public string? Summary { get; init; }
    public IReadOnlyList<string> Genres { get; init; } = [];
    public string? Developer { get; init; }
    public string? Publisher { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public long? IgdbId { get; init; }

    /// <summary>Portrait cover candidates, best first.</summary>
    public IReadOnlyList<string> CoverUrls { get; init; } = [];

    /// <summary>Landscape artwork candidates, best first.</summary>
    public IReadOnlyList<string> BackgroundUrls { get; init; } = [];

    /// <summary>Keeps this instance's values and fills the gaps with <paramref name="fallback"/>.</summary>
    public GameMetadata Merge(GameMetadata? fallback) => fallback is null ? this : new GameMetadata
    {
        Summary = Summary ?? fallback.Summary,
        Genres = Genres.Count > 0 ? Genres : fallback.Genres,
        Developer = Developer ?? fallback.Developer,
        Publisher = Publisher ?? fallback.Publisher,
        ReleaseDate = ReleaseDate ?? fallback.ReleaseDate,
        IgdbId = IgdbId ?? fallback.IgdbId,
        CoverUrls = [.. CoverUrls, .. fallback.CoverUrls],
        BackgroundUrls = [.. BackgroundUrls, .. fallback.BackgroundUrls],
    };
}

public interface IMetadataProvider
{
    bool IsEnabled { get; }

    /// <summary>Lower runs first. Providers can rank themselves higher for queries they answer exactly.</summary>
    int GetPriority(MetadataQuery query);

    Task<GameMetadata?> FetchAsync(MetadataQuery query, CancellationToken cancellationToken);
}

/// <summary>Background enrichment of games with metadata and artwork.</summary>
public interface IMetadataQueue
{
    void Enqueue(int gameId);

    /// <summary>Raised (on a background thread) after a game's metadata was saved.</summary>
    event EventHandler<int>? GameUpdated;
}
