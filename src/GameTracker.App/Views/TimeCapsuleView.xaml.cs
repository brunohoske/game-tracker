using System.Windows;
using System.Windows.Controls;
using GameTracker.App.ViewModels;

namespace GameTracker.App.Views;

public partial class TimeCapsuleView : UserControl
{
    public TimeCapsuleView() => InitializeComponent();

    // The view is recreated each time the tab is selected: refresh so the current month is up to date.
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TimeCapsuleViewModel viewModel)
            await viewModel.ActivateAsync();
    }
}
