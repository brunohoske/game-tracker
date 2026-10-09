using System.Windows.Controls;

namespace GameTracker.App.Views;

public partial class ConfirmDialogView : UserControl
{
    public ConfirmDialogView()
    {
        InitializeComponent();
        Loaded += (_, _) => ConfirmButton.Focus();
    }
}
