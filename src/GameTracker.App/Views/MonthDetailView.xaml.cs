using System.Windows.Controls;
using System.Windows.Input;
using GameTracker.App.ViewModels;

namespace GameTracker.App.Views;

public partial class MonthDetailView : UserControl
{
    public MonthDetailView() => InitializeComponent();

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        // Mouse "back" button returns to the timeline.
        if (e.ChangedButton == MouseButton.XButton1 && DataContext is MonthDetailViewModel viewModel)
        {
            viewModel.BackCommand.Execute(null);
            e.Handled = true;
        }
        base.OnMouseUp(e);
    }
}
