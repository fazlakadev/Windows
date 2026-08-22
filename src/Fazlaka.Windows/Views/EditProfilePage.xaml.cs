using Fazlaka.Windows.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Fazlaka.Windows.Views;

public sealed partial class EditProfilePage : Page
{
    public EditProfileViewModel Vm { get; }

    public EditProfilePage()
    {
        Vm = new EditProfileViewModel();
        InitializeComponent();
        Vm.LoadCommand.Execute(null);
    }
}
