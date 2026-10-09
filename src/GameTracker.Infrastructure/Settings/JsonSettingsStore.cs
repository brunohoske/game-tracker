using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameTracker.Core.Settings;
using Microsoft.Extensions.Logging;

namespace GameTracker.Infrastructure.Settings;

/// <summary>
/// Settings persisted as JSON next to the database. Secrets are encrypted with DPAPI (bound to the Windows user).
/// </summary>
internal sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly byte[] Entropy = "GameTracker.Settings"u8.ToArray();

    private readonly string _path;
    private readonly ILogger<JsonSettingsStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AppSettings _current;

    public JsonSettingsStore(AppPaths paths, ILogger<JsonSettingsStore> logger)
    {
        _path = paths.SettingsPath;
        _logger = logger;
        _current = Load();
    }

    public AppSettings Current => Volatile.Read(ref _current);

    public event EventHandler<AppSettings>? Changed;

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = new SettingsDocument
            {
                StartWithWindows = settings.StartWithWindows,
                SuggestGames = settings.SuggestGames,
                PollIntervalSeconds = settings.PollIntervalSeconds,
                MinimumSessionSeconds = settings.MinimumSessionSeconds,
                SessionMergeGapSeconds = settings.SessionMergeGapSeconds,
                GameFolders = [.. settings.GameFolders],
                IgdbClientId = settings.IgdbClientId,
                IgdbClientSecretProtected = Protect(settings.IgdbClientSecret),
            };

            var temp = _path + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(document, JsonOptions), cancellationToken);
            File.Move(temp, _path, overwrite: true);

            Volatile.Write(ref _current, settings);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, settings);
    }

    private AppSettings Load()
    {
        if (!File.Exists(_path))
            return new AppSettings();

        try
        {
            var document = JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(_path)) ?? new SettingsDocument();
            return new AppSettings
            {
                StartWithWindows = document.StartWithWindows,
                SuggestGames = document.SuggestGames,
                PollIntervalSeconds = document.PollIntervalSeconds,
                MinimumSessionSeconds = document.MinimumSessionSeconds,
                SessionMergeGapSeconds = document.SessionMergeGapSeconds,
                GameFolders = document.GameFolders,
                IgdbClientId = document.IgdbClientId,
                IgdbClientSecret = Unprotect(document.IgdbClientSecretProtected),
            };
        }
        catch (Exception ex) when (ex is JsonException or IOException or CryptographicException or FormatException)
        {
            _logger.LogWarning(ex, "Settings file is invalid; using defaults");
            return new AppSettings();
        }
    }

    private static string? Protect(string? secret) =>
        string.IsNullOrEmpty(secret)
            ? null
            : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser));

    private static string? Unprotect(string? protectedSecret) =>
        string.IsNullOrEmpty(protectedSecret)
            ? null
            : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedSecret), Entropy, DataProtectionScope.CurrentUser));

    /// <summary>On-disk shape (decoupled from <see cref="AppSettings"/> so secrets never hit the disk in clear).</summary>
    private sealed class SettingsDocument
    {
        private static readonly AppSettings Defaults = new();

        public bool StartWithWindows { get; set; } = Defaults.StartWithWindows;
        public bool SuggestGames { get; set; } = Defaults.SuggestGames;
        public int PollIntervalSeconds { get; set; } = Defaults.PollIntervalSeconds;
        public int MinimumSessionSeconds { get; set; } = Defaults.MinimumSessionSeconds;
        public int SessionMergeGapSeconds { get; set; } = Defaults.SessionMergeGapSeconds;
        public List<string> GameFolders { get; set; } = [];
        public string? IgdbClientId { get; set; }
        public string? IgdbClientSecretProtected { get; set; }
    }
}
