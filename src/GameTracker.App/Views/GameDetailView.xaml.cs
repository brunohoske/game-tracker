using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GameTracker.App.Views;

public partial class GameDetailView : UserControl
{
    public GameDetailView() => InitializeComponent();

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        // Mouse "back" button returns to the library.
        if (e.ChangedButton == MouseButton.XButton1 && DataContext is ViewModels.GameDetailViewModel viewModel)
        {
            viewModel.BackCommand.Execute(null);
            e.Handled = true;
        }
        base.OnMouseUp(e);
    }

    private void OnNameEditorVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            NameEditor.Focus();
            NameEditor.SelectAll();
        }
    }
}
