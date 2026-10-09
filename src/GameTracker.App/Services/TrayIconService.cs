using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using GameTracker.Core.Tracking;
using H.NotifyIcon;

namespace GameTracker.App.Services;

/// <summary>Notification-area icon: opens the app, shows what is being played, announces suggestions, exits.</summary>
public sealed class TrayIconService(WindowManager windows, IActiveGamesMonitor activeGames) : IDisposable
{
    private TaskbarIcon? _icon;
    private MenuItem? _statusItem;

    public event EventHandler? ExitRequested;

    public void Show()
    {
        _statusItem = new MenuItem { IsEnabled = false };

        var open = new MenuItem { Header = "Abrir GameTracker", FontWeight = FontWeights.SemiBold };
        open.Click += (_, _) => windows.ShowMain();

        var exit = new MenuItem { Header = "Sair" };
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
            ContextMenu = new ContextMenu { Items = { open, _statusItem, new Separator(), exit } },
            MenuActivation = H.NotifyIcon.Core.PopupActivationMode.RightClick,
            NoLeftClickDelay = true,
        };
        _icon.TrayLeftMouseUp += (_, _) => windows.ShowMain();
        _icon.TrayBalloonTipClicked += (_, _) => windows.ShowMain();
        _icon.ForceCreate(enablesEfficiencyMode: false);

        activeGames.ActiveGamesChanged += OnActiveGamesChanged;
        activeGames.GameSuggested += OnGameSuggested;
        UpdateStatus();
    }

    public void Dispose()
    {
        activeGames.ActiveGamesChanged -= OnActiveGamesChanged;
        activeGames.GameSuggested -= OnGameSuggested;
        _icon?.Dispose();
        _icon = null;
    }

    private void OnActiveGamesChanged(object? sender, EventArgs e) =>
        Application.Current?.Dispatcher.BeginInvoke(UpdateStatus);

    /// <summary>Windows queues it while a fullscreen game runs (focus assist) and shows it afterwards.</summary>
    private void OnGameSuggested(object? sender, ActiveGame game) =>
        Application.Current?.Dispatcher.BeginInvoke(() => _icon?.ShowNotification(
            "Isso é um jogo?",
            $"\"{game.Name}\" está rodando em tela cheia. O tempo já está sendo registrado; clique para confirmar."));

    private void UpdateStatus()
    {
        if (_icon is null || _statusItem is null)
            return;

        var playing = activeGames.ActiveGames.Select(g => g.Name).ToArray();
        var status = playing.Length == 0 ? "Nenhum jogo em execução" : "Jogando: " + string.Join(", ", playing);
        _statusItem.Header = status;
        _icon.ToolTipText = "GameTracker — " + status;
    }
}
