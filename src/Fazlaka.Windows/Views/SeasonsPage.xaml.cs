using System;
using System.Linq;
using Fazlaka.Windows.Models;
using Fazlaka.Windows.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Fazlaka.Windows.Views;

public sealed partial class SeasonsPage : Page
{
    public SeasonsViewModel ViewModel { get; }

    public SeasonsPage()
    {
        ViewModel = new SeasonsViewModel();
        InitializeComponent();
        ViewModel.LoadSeasonsCommand.Execute(null);
    }

    public void SelectSeasonById(string? seasonId)
    {
        if (string.IsNullOrEmpty(seasonId))
        {
            return;
        }

        var match = ViewModel.Seasons.FirstOrDefault(s => s.Id == seasonId);
        if (match is not null)
        {
            ViewModel.SelectedSeason = match;
        }
    }

    private void OnSeasonSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is Season season)
        {
            ViewModel.SelectSeasonCommand.Execute(season);
        }
    }

    private void OnRowTapped(object sender, TappedRoutedEventArgs e)
    {
        PlayFrom(sender);
    }

    private void OnPlayEpisodeClick(object sender, RoutedEventArgs e)
    {
        PlayFrom(sender);
    }

    private void PlayFrom(object sender)
    {
        if ((sender as FrameworkElement)?.DataContext is Episode episode)
        {
            ViewModel.PlayEpisodeCommand.Execute(episode);
        }
    }
}
