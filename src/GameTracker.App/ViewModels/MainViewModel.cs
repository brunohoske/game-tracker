using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTracker.App.Services;

namespace GameTracker.App.ViewModels;

/// <summary>
/// Shell: top navigation (future tabs plug in here) and the modal overlay.
/// Page view models are owned (and disposed) by the window's DI scope.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly Func<SettingsViewModel> _settingsFactory;

    public MainViewModel(GamesViewModel games, TimeCapsuleViewModel timeCapsule, OverlayHost overlay, Func<SettingsViewModel> settingsFactory)
    {
        _settingsFactory = settingsFactory;
        Overlay = overlay;
        Tabs =
        [
            new NavigationItem("JOGOS", games),
            new NavigationItem("CÁPSULA DO TEMPO", timeCapsule),
        ];
        SelectedTab = Tabs[0];
    }

    public IReadOnlyList<NavigationItem> Tabs { get; }

    public OverlayHost Overlay { get; }

    [ObservableProperty]
    public partial NavigationItem SelectedTab { get; set; }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var settings = _settingsFactory();
        Overlay.Content = settings;
        await settings.LoadAsync();
    }

    public void CancelOverlay() => (Overlay.Content as IOverlay)?.Cancel();
}
