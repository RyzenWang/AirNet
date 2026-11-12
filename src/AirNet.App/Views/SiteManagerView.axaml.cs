using Avalonia.Controls;
using Avalonia.Interactivity;
using AirNet.App.ViewModels;

namespace AirNet.App.Views;

public partial class SiteManagerView : UserControl
{
    public SiteManagerView()
    {
        InitializeComponent();
    }

    private void OnEditCustomClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SiteLatencyViewModel viewModel
            && sender is Button { DataContext: CustomSiteRow row })
        {
            viewModel.EditCustom(row.Target);
        }
    }

    private void OnDeleteCustomClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SiteLatencyViewModel viewModel
            && sender is Button { DataContext: CustomSiteRow row })
        {
            viewModel.RequestDeleteCustom(row.Target);
        }
    }
}
