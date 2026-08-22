using Fazlaka.Windows.Models;
using Fazlaka.Windows.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Fazlaka.Windows.Views;

public sealed partial class HistoryPage : Page
{
    public HistoryViewModel Vm { get; }

    public HistoryPage()
    {
        Vm = new HistoryViewModel();
        InitializeComponent();
        Vm.LoadCommand.Execute(null);
    }

    private void OnItemTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement fe && fe.Tag is HistoryItem item)
        {
            Vm.PlayItemCommand.Execute(item);
        }
    }

    private void OnItemPlayClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement fe && fe.Tag is HistoryItem item)
        {
            Vm.PlayItemCommand.Execute(item);
        }
    }
}
