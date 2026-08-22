using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Fazlaka.Windows.ViewModels;

namespace Fazlaka.Windows.Views;

public sealed partial class PlaylistsPage : Page
{
    public PlaylistsViewModel ViewModel { get; }

    public string Greeting => DateTime.Now.Hour switch
    {
        < 6 => "مساء الخير",
        < 12 => "صباح الخير",
        < 17 => "مساء الخير",
        _ => "مساء الخير"
    };

    public PlaylistsPage()
    {
        ViewModel = App.Services.Get<PlaylistsViewModel>();
        InitializeComponent();
        ViewModel.LoadDataCommand.Execute(null);
    }

    private void OnRefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
    {
        ViewModel.RefreshCommand.Execute(null);
    }
}
