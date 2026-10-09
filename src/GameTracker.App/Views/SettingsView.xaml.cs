using System.Windows;
using System.Windows.Controls;
using GameTracker.App.ViewModels;

namespace GameTracker.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

    // PasswordBox.Password is intentionally not bindable; bridge it here.
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        SecretBox.Password = ViewModel?.IgdbClientSecret ?? string.Empty;

    private void OnSecretChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
            viewModel.IgdbClientSecret = SecretBox.Password;
    }
}
