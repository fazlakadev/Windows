using Fazlaka.Windows.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Fazlaka.Windows.Views;

public sealed partial class SecurityPage : Page
{
    public SecurityViewModel Vm { get; }

    public SecurityPage()
    {
        Vm = new SecurityViewModel();
        InitializeComponent();
        NewPinBox.PasswordChanged += (_, _) => Vm.PinInput = NewPinBox.Password;
        RemovePinBox.PasswordChanged += (_, _) => Vm.CurrentPinInput = RemovePinBox.Password;
    }
}
