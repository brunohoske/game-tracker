using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GameTracker.App.ViewModels;

namespace GameTracker.App.Views;

public partial class GamesView : UserControl
{
    public GamesView() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is GamesViewModel viewModel)
            await viewModel.ActivateAsync();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control && SearchBox.IsVisible)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }
}
