using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GameTracker.Core.Common;
using GameTracker.Core.Metadata;
using GameTracker.Core.Settings;

namespace GameTracker.Infrastructure.Metadata;

/// <summary>
/// IGDB (Twitch) catalog: the broadest PC game database, used when credentials are configured.
/// Docs: https://api-docs.igdb.com
/// </summary>
internal sealed class IgdbMetadataProvider(HttpClient http, TwitchTokenProvider tokens, ISettingsStore settings) : IMetadataProvider
{
    private const string ImageBaseUrl = "https://images.igdb.com/igdb/image/upload";
    private const string Fields =
        "fields name,summary,first_release_date,genres.name,cover.image_id,artworks.image_id,screenshots.image_id," +
        "involved_companies.developer,involved_companies.publisher,involved_companies.company.name;";

    public bool IsEnabled => settings.Current.HasIgdbCredentials;

    public int GetPriority(MetadataQuery query) => query.SteamAppId is null ? 0 : 10;

    public async Task<GameMetadata?> FetchAsync(MetadataQuery query, CancellationToken cancellationToken)
    {
        var body = $"search \"{query.Name.Replace("\"", string.Empty, StringComparison.Ordinal)}\"; {Fields} where version_parent = null; limit 10;";
        var games = await PostAsync(body, cancellationToken, retryOnUnauthorized: true);
        if (games is not { Length: > 0 })
            return null;

        var wanted = TextUtil.NormalizeTitle(query.Name);
        var game = games.FirstOrDefault(g => TextUtil.NormalizeTitle(g.Name ?? string.Empty) == wanted) ?? games[0];

        return new GameMetadata
        {
            IgdbId = game.Id,
            Summary = game.Summary,
            Genres = game.Genres?.Select(g => g.Name).OfType<string>().ToArray() ?? [],
            Developer = game.InvolvedCompanies?.FirstOrDefault(c => c.Developer)?.Company?.Name,
            Publisher = game.InvolvedCompanies?.FirstOrDefault(c => c.Publisher)?.Company?.Name,
            ReleaseDate = game.FirstReleaseDate is { } seconds
                ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime)
                : null,
            CoverUrls = game.Cover?.ImageId is { } cover ? [$"{ImageBaseUrl}/t_cover_big_2x/{cover}.jpg"] : [],
            BackgroundUrls =
            [
                .. (game.Artworks ?? []).Concat(game.Screenshots ?? [])
                    .Select(i => i.ImageId).OfType<string>()
                    .Take(3)
                    .Select(id => $"{ImageBaseUrl}/t_1080p/{id}.jpg"),
            ],
        };
    }

    private async Task<IgdbGame[]?> PostAsync(string body, CancellationToken cancellationToken, bool retryOnUnauthorized)
    {
        var current = settings.Current;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games")
        {
            Content = new StringContent(body),
        };
        request.Headers.Add("Client-ID", current.IgdbClientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(cancellationToken));

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized && retryOnUnauthorized)
        {
            tokens.Invalidate();
            return await PostAsync(body, cancellationToken, retryOnUnauthorized: false);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IgdbGame[]>(cancellationToken);
    }

    private sealed record IgdbGame(
        long Id,
        string? Name,
        string? Summary,
        [property: JsonPropertyName("first_release_date")] long? FirstReleaseDate,
        IgdbNamed[]? Genres,
        IgdbImage? Cover,
        IgdbImage[]? Artworks,
        IgdbImage[]? Screenshots,
        [property: JsonPropertyName("involved_companies")] IgdbInvolvedCompany[]? InvolvedCompanies);

    private sealed record IgdbNamed(string? Name);

    private sealed record IgdbImage([property: JsonPropertyName("image_id")] string? ImageId);

    private sealed record IgdbInvolvedCompany(bool Developer, bool Publisher, IgdbNamed? Company);
}

/// <summary>App access token (client credentials) for IGDB, cached until shortly before it expires.</summary>
internal sealed class TwitchTokenProvider(HttpClient http, ISettingsStore settings, TimeProvider time)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private string? _tokenClientId;
    private DateTimeOffset _expiresAt;

    public void Invalidate() => _token = null;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = settings.Current;
            if (_token is not null && _tokenClientId == current.IgdbClientId && time.GetUtcNow() < _expiresAt)
                return _token;

            var url = "https://id.twitch.tv/oauth2/token" +
                      $"?client_id={Uri.EscapeDataString(current.IgdbClientId ?? string.Empty)}" +
                      $"&client_secret={Uri.EscapeDataString(current.IgdbClientSecret ?? string.Empty)}" +
                      "&grant_type=client_credentials";

            using var response = await http.PostAsync(url, content: null, cancellationToken);
            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                        ?? throw new InvalidOperationException("Empty token response from Twitch.");

            _token = token.AccessToken;
            _tokenClientId = current.IgdbClientId;
            _expiresAt = time.GetUtcNow().AddSeconds(token.ExpiresIn).AddMinutes(-5);
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] long ExpiresIn);
}
