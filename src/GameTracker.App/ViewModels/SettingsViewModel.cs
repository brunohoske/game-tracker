using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTracker.App.Services;
using GameTracker.Core.Library;
using GameTracker.Core.Reporting;
using GameTracker.Core.Settings;
using GameTracker.Infrastructure.Settings;

namespace GameTracker.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject, IOverlay
{
    private const string IgdbDocsUrl = "https://api-docs.igdb.com/#account-creation";

    private readonly ISettingsStore _settings;
    private readonly StartupRegistration _startup;
    private readonly IGameLibrary _library;
    private readonly IDialogService _dialogs;
    private readonly OverlayHost _overlay;

    public SettingsViewModel(ISettingsStore settings, StartupRegistration startup, IGameLibrary library, IDialogService dialogs, OverlayHost overlay)
    {
        _settings = settings;
        _startup = startup;
        _library = library;
        _dialogs = dialogs;
        _overlay = overlay;

        var current = settings.Current;
        StartWithWindows = current.StartWithWindows;
        SuggestGames = current.SuggestGames;
        PollIntervalSeconds = current.PollIntervalSeconds;
        MinimumSessionMinutes = current.MinimumSessionSeconds / 60.0;
        MergeGapMinutes = current.SessionMergeGapSeconds / 60.0;
        GameFolders = [.. current.GameFolders];
        IgdbClientId = current.IgdbClientId ?? string.Empty;
        IgdbClientSecret = current.IgdbClientSecret ?? string.Empty;
    }

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial bool SuggestGames { get; set; }

    [ObservableProperty]
    public partial double PollIntervalSeconds { get; set; }

    [ObservableProperty]
    public partial double MinimumSessionMinutes { get; set; }

    [ObservableProperty]
    public partial double MergeGapMinutes { get; set; }

    [ObservableProperty]
    public partial string IgdbClientId { get; set; }

    public string IgdbClientSecret { get; set; }

    public ObservableCollection<string> GameFolders { get; }

    public ObservableCollection<GameInfo> IgnoredGames { get; } = [];

    public async Task LoadAsync()
    {
        foreach (var game in await Task.Run(() => _library.GetIgnoredGamesAsync()))
            IgnoredGames.Add(game);
    }

    [RelayCommand]
    private void AddFolder()
    {
        if (_dialogs.PickFolder() is { } folder && !GameFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
            GameFolders.Add(folder);
    }

    [RelayCommand]
    private void RemoveFolder(string folder) => GameFolders.Remove(folder);

    [RelayCommand]
    private async Task RestoreGameAsync(GameInfo game)
    {
        await Task.Run(() => _library.SetIgnoredAsync(game.Id, false));
        IgnoredGames.Remove(game);
    }

    [RelayCommand]
    private static void OpenIgdbDocs() => Process.Start(new ProcessStartInfo { FileName = IgdbDocsUrl, UseShellExecute = true });

    [RelayCommand]
    private async Task SaveAsync()
    {
        var updated = _settings.Current with
        {
            StartWithWindows = StartWithWindows,
            SuggestGames = SuggestGames,
            PollIntervalSeconds = (int)PollIntervalSeconds,
            MinimumSessionSeconds = (int)Math.Round(MinimumSessionMinutes * 60),
            SessionMergeGapSeconds = (int)Math.Round(MergeGapMinutes * 60),
            GameFolders = [.. GameFolders],
            IgdbClientId = NullIfBlank(IgdbClientId),
            IgdbClientSecret = NullIfBlank(IgdbClientSecret),
        };

        await _settings.SaveAsync(updated);
        _startup.Apply(updated.StartWithWindows);
        _overlay.Close();
    }

    [RelayCommand]
    public void Cancel() => _overlay.Close();

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
