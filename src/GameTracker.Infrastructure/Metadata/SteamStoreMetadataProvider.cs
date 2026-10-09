using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using GameTracker.Core.Common;
using GameTracker.Core.Metadata;

namespace GameTracker.Infrastructure.Metadata;

/// <summary>
/// Public Steam Store API (no key required). Exact for Steam games; for others it searches the store by name,
/// which covers most PC games sold on Steam as well.
/// </summary>
internal sealed class SteamStoreMetadataProvider(HttpClient http) : IMetadataProvider
{
    private static readonly string[] AssetHosts =
    [
        "https://shared.steamstatic.com/store_item_assets/steam/apps",
        "https://cdn.cloudflare.steamstatic.com/steam/apps",
    ];

    private static readonly CultureInfo[] DateCultures = [CultureInfo.GetCultureInfo("pt-BR"), CultureInfo.InvariantCulture];
    private static readonly string[] DateFormats = ["d/MMM/yyyy", "d 'de' MMM'.' 'de' yyyy", "d 'de' MMM 'de' yyyy", "d MMM, yyyy", "MMM d, yyyy"];

    public bool IsEnabled => true;

    public int GetPriority(MetadataQuery query) => query.SteamAppId is null ? 20 : 0;

    public async Task<GameMetadata?> FetchAsync(MetadataQuery query, CancellationToken cancellationToken)
    {
        var appId = query.SteamAppId ?? await SearchAppIdAsync(query.Name, cancellationToken);
        if (appId is null)
            return null;

        using var document = await http.GetFromJsonAsync<JsonDocument>(
            $"https://store.steampowered.com/api/appdetails?appids={Uri.EscapeDataString(appId)}&l=brazilian",
            cancellationToken);

        if (document is null
            || !document.RootElement.TryGetProperty(appId, out var app)
            || !app.TryGetProperty("success", out var success) || !success.GetBoolean()
            || !app.TryGetProperty("data", out var data))
        {
            return null;
        }

        return new GameMetadata
        {
            Summary = GetString(data, "short_description"),
            Genres = GetArray(data, "genres").Select(g => GetString(g, "description")).OfType<string>().ToArray(),
            Developer = GetArray(data, "developers").Select(d => d.GetString()).FirstOrDefault(),
            Publisher = GetArray(data, "publishers").Select(p => p.GetString()).FirstOrDefault(),
            ReleaseDate = data.TryGetProperty("release_date", out var release) ? ParseDate(GetString(release, "date")) : null,
            CoverUrls = [.. AssetHosts.Select(host => $"{host}/{appId}/library_600x900_2x.jpg")],
            BackgroundUrls =
            [
                .. AssetHosts.Select(host => $"{host}/{appId}/library_hero.jpg"),
                .. new[] { GetString(data, "background_raw"), GetString(data, "header_image") }.OfType<string>(),
            ],
        };
    }

    private async Task<string?> SearchAppIdAsync(string name, CancellationToken cancellationToken)
    {
        using var document = await http.GetFromJsonAsync<JsonDocument>(
            $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(name)}&l=english&cc=US",
            cancellationToken);
        if (document is null)
            return null;

        // Only accept an exact title match: a wrong game is worse than no metadata.
        var wanted = TextUtil.NormalizeTitle(name);
        return GetArray(document.RootElement, "items")
            .Where(item => GetString(item, "name") is { } title && TextUtil.NormalizeTitle(title) == wanted)
            .Select(item => item.TryGetProperty("id", out var id) ? id.GetRawText() : null)
            .FirstOrDefault();
    }

    private static DateOnly? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        foreach (var culture in DateCultures)
        {
            if (DateTime.TryParseExact(text, DateFormats, culture, DateTimeStyles.AllowWhiteSpaces, out var date))
                return DateOnly.FromDateTime(date);
        }
        return null;
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    private static IEnumerable<JsonElement> GetArray(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];
}
