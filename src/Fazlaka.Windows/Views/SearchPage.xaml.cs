using System;
using Fazlaka.Windows.Models;
using Fazlaka.Windows.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Fazlaka.Windows.Views;

public sealed partial class SearchPage : Page
{
    private bool _focusedOnce;

    public SearchViewModel ViewModel { get; }

    public SearchPage()
    {
        ViewModel = new SearchViewModel();
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_focusedOnce)
        {
            return;
        }

        _focusedOnce = true;
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Enter:
                ViewModel.SearchCommand.Execute(null);
                e.Handled = true;
                break;
            case VirtualKey.Escape:
                ViewModel.ClearSearchCommand.Execute(null);
                e.Handled = true;
                break;
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
