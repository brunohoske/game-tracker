using CommunityToolkit.Mvvm.Input;

namespace GameTracker.App.ViewModels;

public sealed partial class ConfirmDialogViewModel(string title, string message, string confirmText, bool isDestructive, Action close) : IOverlay
{
    private readonly TaskCompletionSource<bool> _result = new();

    public string Title { get; } = title;
    public string Message { get; } = message;
    public string ConfirmText { get; } = confirmText;
    public bool IsDestructive { get; } = isDestructive;

    public Task<bool> Result => _result.Task;

    [RelayCommand]
    private void Confirm() => Complete(true);

    [RelayCommand]
    public void Cancel() => Complete(false);

    private void Complete(bool confirmed)
    {
        close();
        _result.TrySetResult(confirmed);
    }
}
