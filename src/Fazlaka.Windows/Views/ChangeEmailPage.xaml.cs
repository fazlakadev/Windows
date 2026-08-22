using Fazlaka.Windows.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Fazlaka.Windows.Views;

public sealed partial class ChangeEmailPage : Page
{
    public ChangeEmailViewModel Vm { get; }

    public ChangeEmailPage()
    {
        Vm = new ChangeEmailViewModel();
        InitializeComponent();
    }
}
