using Fazlaka.Windows.Models;
using Fazlaka.Windows.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Fazlaka.Windows.Views;

public sealed partial class SessionsPage : Page
{
    public SessionsViewModel Vm { get; }

    public SessionsPage()
    {
        Vm = new SessionsViewModel();
        InitializeComponent();
        Vm.LoadCommand.Execute(null);
    }

    private void OnRevokeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Session session)
        {
            Vm.RevokeSessionCommand.Execute(session);
        }
    }
}
