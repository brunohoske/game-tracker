using System.Threading.Channels;
using GameTracker.Core.Metadata;
using GameTracker.Core.Settings;
using GameTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameTracker.Infrastructure.Metadata;

/// <summary>
/// Fills games with description, genres, companies, cover and background artwork, one game at a time,
/// querying every enabled provider by priority and merging their answers.
/// </summary>
internal sealed class MetadataEnrichmentService(
    IDbContextFactory<GameTrackerDbContext> contextFactory,
    IEnumerable<IMetadataProvider> providers,
    ImageStore images,
    ISettingsStore settings,
    TimeProvider time,
    ILogger<MetadataEnrichmentService> logger) : BackgroundService, IMetadataQueue
{
    private static readonly TimeSpan DelayBetweenGames = TimeSpan.FromMilliseconds(400);

    private readonly Channel<int> _queue = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = true });
    private string? _lastIgdbClientId = settings.Current.IgdbClientId;

    public event EventHandler<int>? GameUpdated;

    public void Enqueue(int gameId) => _queue.Writer.TryWrite(gameId);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() => RunAsync(stoppingToken), stoppingToken);

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        await EnqueuePendingAsync(onlyWithoutCover: false, stoppingToken);
        settings.Changed += OnSettingsChanged;
        try
        {
            await foreach (var gameId in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await EnrichAsync(gameId, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Metadata update failed for game {GameId}", gameId);
                }
                await Task.Delay(DelayBetweenGames, time, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            settings.Changed -= OnSettingsChanged;
        }
    }

    private async Task EnrichAsync(int gameId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);
        if (game is null || game.IsIgnored)
            return;

        var query = new MetadataQuery(game.Name, game.Source, game.ExternalId);
        GameMetadata? metadata = null;
        var failed = false;

        foreach (var provider in providers.Where(p => p.IsEnabled).OrderBy(p => p.GetPriority(query)))
        {
            try
            {
                var result = await provider.FetchAsync(query, cancellationToken);
                metadata = metadata?.Merge(result) ?? result;
            }
            catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                failed = true;
                logger.LogInformation(ex, "{Provider} unavailable for \"{Game}\"", provider.GetType().Name, game.Name);
            }
        }

        // Offline: keep it pending so it is retried on the next start.
        if (metadata is null && failed)
            return;

        if (metadata is not null)
        {
            game.Summary = metadata.Summary ?? game.Summary;
            game.Genres = metadata.Genres.Count > 0 ? [.. metadata.Genres.Distinct().Take(5)] : game.Genres;
            game.Developer = metadata.Developer ?? game.Developer;
            game.Publisher = metadata.Publisher ?? game.Publisher;
            game.ReleaseDate = metadata.ReleaseDate ?? game.ReleaseDate;
            game.IgdbId = metadata.IgdbId ?? game.IgdbId;

            // Portrait art first; a landscape image cropped is still better than an empty card.
            game.CoverImagePath = await images.DownloadFirstAsync(gameId, "cover", [.. metadata.CoverUrls, .. metadata.BackgroundUrls], cancellationToken)
                                  ?? game.CoverImagePath;
            game.BackgroundImagePath = await images.DownloadFirstAsync(gameId, "background", metadata.BackgroundUrls, cancellationToken)
                                       ?? game.BackgroundImagePath;
        }

        game.MetadataUpdatedAtUtc = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        GameUpdated?.Invoke(this, gameId);
    }

    private async Task EnqueuePendingAsync(bool onlyWithoutCover, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var allIds = await db.Games.Select(g => g.Id).ToListAsync(cancellationToken);
        images.RemoveOrphans(allIds.ToHashSet());

        var pending = await db.Games
            .Where(g => !g.IsIgnored && (onlyWithoutCover ? g.CoverImagePath == null : g.MetadataUpdatedAtUtc == null))
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in pending)
            Enqueue(id);
    }

    private void OnSettingsChanged(object? sender, AppSettings newSettings)
    {
        if (!newSettings.HasIgdbCredentials || newSettings.IgdbClientId == _lastIgdbClientId)
            return;

        // New IGDB credentials: give games without artwork another chance.
        _lastIgdbClientId = newSettings.IgdbClientId;
        _ = Task.Run(async () =>
        {
            try
            {
                await EnqueuePendingAsync(onlyWithoutCover: true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not queue games for metadata refresh");
            }
        });
    }
}
