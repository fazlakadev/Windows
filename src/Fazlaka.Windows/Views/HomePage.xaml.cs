using System;
using System.ComponentModel;
using Fazlaka.Windows.Models;
using Fazlaka.Windows.Services;
using Fazlaka.Windows.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Fazlaka.Windows.Views;

public sealed partial class HomePage : Page
{
    private readonly SettingsService _settingsService;
    private global::Windows.Foundation.Deferral? _refreshDeferral;
    private bool _pulseRunning;

    public HomeViewModel ViewModel { get; }

    public string Greeting { get; }

    public HomePage()
    {
        _settingsService = App.Services.Get<SettingsService>();
        ViewModel = new HomeViewModel();

        var name = _settingsService.UserName;
        Greeting = string.IsNullOrWhiteSpace(name) ? "مرحباً بك" : $"مرحباً، {name}";

        InitializeComponent();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.LoadDataCommand.Execute(null);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.IsLoading))
        {
            UpdateSkeleton();
        }
        else if (e.PropertyName == nameof(HomeViewModel.IsRefreshing))
        {
            if (!ViewModel.IsRefreshing)
            {
                CompleteRefresh();
            }
        }
    }

    private void UpdateSkeleton()
    {
        if (ViewModel.IsLoading)
        {
            SkeletonRoot.Visibility = Visibility.Visible;
            if (!_pulseRunning && Resources["Pulse"] is Microsoft.UI.Xaml.Media.Animation.Storyboard pulse)
            {
                pulse.Begin();
                _pulseRunning = true;
            }
        }
        else
        {
            if (_pulseRunning && Resources["Pulse"] is Microsoft.UI.Xaml.Media.Animation.Storyboard pulse)
            {
                pulse.Stop();
            }

            _pulseRunning = false;
            SkeletonRoot.Visibility = Visibility.Collapsed;
        }
    }

    private void OnRefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
    {
        if (ViewModel.IsLoading)
        {
            return;
        }

        _refreshDeferral = args.GetDeferral();
        ViewModel.RefreshCommand.Execute(null);
    }

    private void CompleteRefresh()
    {
        _refreshDeferral?.Complete();
        _refreshDeferral = null;
    }

    private void OnLatestWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(LatestScroller).Properties.MouseWheelDelta;
        LatestScroller.ChangeView(LatestScroller.HorizontalOffset - delta, null, null);
        e.Handled = true;
    }

    private void OnPlayEpisodeClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Episode episode)
        {
            ViewModel.PlayEpisodeCommand.Execute(episode);
        }
    }
}
