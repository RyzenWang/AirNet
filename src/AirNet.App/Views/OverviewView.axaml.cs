using Avalonia.Controls;
using Avalonia.Interactivity;
using AirNet.App.ViewModels;

namespace AirNet.App.Views;

public partial class OverviewView : UserControl
{
    public OverviewView()
    {
        InitializeComponent();
    }

    private async void OnShareCardClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OverviewViewModel viewModel
            || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var window = new ShareCardWindow
        {
            DataContext = new ShareCardViewModel(viewModel.Shell.View),
        };
        await window.ShowDialog(owner);
    }
}
