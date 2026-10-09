using System.Windows;
using System.Windows.Input;
using GameTracker.App.ViewModels;

namespace GameTracker.App.Views;

/// <summary>Borderless shell window. Code-behind only handles window chrome concerns.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, _) =>
        {
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "" : "";
            MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Restaurar" : "Maximizar";
        };
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && ViewModel?.Overlay.Content is not null)
        {
            ViewModel.CancelOverlay();
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }

    private void OnOverlayBackgroundClick(object sender, MouseButtonEventArgs e) => ViewModel?.CancelOverlay();

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>Closing only releases the window: tracking keeps running in the tray.</summary>
    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
