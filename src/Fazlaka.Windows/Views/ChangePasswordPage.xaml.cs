using Fazlaka.Windows.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Fazlaka.Windows.Views;

public sealed partial class ChangePasswordPage : Page
{
    public ChangePasswordViewModel Vm { get; }

    public ChangePasswordPage()
    {
        Vm = new ChangePasswordViewModel();
        InitializeComponent();
        CurrentPwBox.PasswordChanged += (_, _) => Vm.CurrentPassword = CurrentPwBox.Password;
        NewPwBox.PasswordChanged += (_, _) => Vm.NewPassword = NewPwBox.Password;
        ConfirmPwBox.PasswordChanged += (_, _) => Vm.ConfirmPassword = ConfirmPwBox.Password;
    }
}
