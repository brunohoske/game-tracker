using CommunityToolkit.Mvvm.ComponentModel;
using GameTracker.App.ViewModels;
using Microsoft.Win32;

namespace GameTracker.App.Services;

/// <summary>Hosts the modal overlay (settings, confirmations) drawn inside the main window.</summary>
public sealed partial class OverlayHost : ObservableObject
{
    [ObservableProperty]
    public partial object? Content { get; set; }

    public void Close() => Content = null;
}

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false);

    string? PickExecutable();

    string? PickFolder();
}

internal sealed class DialogService(OverlayHost overlay) : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false)
    {
        var dialog = new ConfirmDialogViewModel(title, message, confirmText, destructive, overlay.Close);
        overlay.Content = dialog;
        return dialog.Result;
    }

    public string? PickExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecione o executável do jogo",
            Filter = "Executáveis (*.exe)|*.exe",
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Selecione uma pasta de jogos" };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
