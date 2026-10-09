using System.Globalization;
using Microsoft.Extensions.Logging;

namespace GameTracker.Infrastructure.Metadata;

/// <summary>Local cache of game artwork: images/{gameId}/{name}.jpg.</summary>
public sealed class ImageStore(AppPaths paths, HttpClient http, ILogger<ImageStore> logger)
{
    /// <summary>Downloads the first candidate URL that returns an image. Returns the local path or null.</summary>
    public async Task<string?> DownloadFirstAsync(int gameId, string name, IEnumerable<string> urls, CancellationToken cancellationToken)
    {
        foreach (var url in urls.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.Ordinal) != true)
                    continue;

                var directory = paths.GameImagesDirectory(gameId);
                Directory.CreateDirectory(directory);
                var target = Path.Combine(directory, name + ".jpg");
                var temp = target + ".tmp";

                await using (var file = File.Create(temp))
                    await response.Content.CopyToAsync(file, cancellationToken);

                File.Move(temp, target, overwrite: true);
                return target;
            }
            catch (HttpRequestException ex)
            {
                logger.LogDebug(ex, "Image download failed: {Url}", url);
            }
        }
        return null;
    }

    public void DeleteGameImages(int gameId)
    {
        try
        {
            var directory = paths.GameImagesDirectory(gameId);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not delete images of game {GameId}", gameId);
        }
    }

    /// <summary>Removes image folders of games that no longer exist.</summary>
    public void RemoveOrphans(IReadOnlySet<int> existingGameIds)
    {
        foreach (var directory in Directory.EnumerateDirectories(paths.ImagesDirectory))
        {
            if (int.TryParse(Path.GetFileName(directory), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                && !existingGameIds.Contains(id))
            {
                DeleteGameImages(id);
            }
        }
    }
}
