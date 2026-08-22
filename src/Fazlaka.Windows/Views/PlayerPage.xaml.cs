using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Fazlaka.Windows.Services;
using Fazlaka.Windows.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using VirtualKey = Windows.System.VirtualKey;

namespace Fazlaka.Windows.Views;

public sealed partial class PlayerPage : Page
{
    private readonly AudioPlayerService _audioPlayer;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
    private bool _attached;
    private bool _seekDragging;

    public PlayerViewModel Player { get; }

    public PlayerPage()
    {
        _audioPlayer = App.Services.Get<AudioPlayerService>();
        _dispatcherQueue = global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Player = MainWindow.SharedPlayer;
        InitializeComponent();

        SeekSlider.AddHandler(PointerPressedEvent, new PointerEventHandler(OnSeekPointerPressed), true);
        SeekSlider.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnSeekPointerReleased), true);
        SeekSlider.AddHandler(PointerCanceledEvent, new PointerEventHandler(OnSeekPointerReleased), true);
        SeekSlider.AddHandler(PointerCaptureLostEvent, new PointerEventHandler(OnSeekPointerReleased), true);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (_attached)
        {
            return;
        }

        _attached = true;
        Player.PropertyChanged += OnPlayerPropertyChanged;
        _audioPlayer.EpisodeChanged += OnEpisodeChanged;

        SeekSlider.Value = Player.PositionSeconds;
        UpdateQueueLabel();
        Focus(FocusState.Programmatic);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        if (!_attached)
        {
            return;
        }

        _attached = false;
        Player.PropertyChanged -= OnPlayerPropertyChanged;
        _audioPlayer.EpisodeChanged -= OnEpisodeChanged;
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerViewModel.PositionSeconds) && !_seekDragging)
        {
            SeekSlider.Value = Player.PositionSeconds;
        }
    }

    private void OnEpisodeChanged(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(UpdateQueueLabel);
    }

    private void OnSeekPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _seekDragging = true;
    }

    private void OnSeekPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_seekDragging)
        {
            return;
        }

        _seekDragging = false;
        Player.SeekCommand.Execute(SeekSlider.Value);
    }

    private void OnBack15Clicked(object sender, RoutedEventArgs e) => SkipBy(-15);

    private void OnForward15Clicked(object sender, RoutedEventArgs e) => SkipBy(15);

    private void SkipBy(double seconds)
    {
        var target = Math.Clamp(Player.PositionSeconds + seconds, 0, Player.SliderMaximum);
        Player.SeekCommand.Execute(target);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnPageKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Escape:
                Close();
                e.Handled = true;
                break;
            case VirtualKey.Space:
                if (e.OriginalSource is not ButtonBase)
                {
                    Player.PlayPauseCommand.Execute(null);
                    e.Handled = true;
                }
                break;
            case VirtualKey.Right:
                SkipBy(10);
                e.Handled = true;
                break;
            case VirtualKey.Left:
                SkipBy(-10);
                e.Handled = true;
                break;
        }
    }

    private bool _isLiked;

    private async void OnLikeClicked(object sender, RoutedEventArgs e)
    {
        var ep = Player.CurrentEpisode;
        if (ep is null || ep.Id is null) return;

        _isLiked = !_isLiked;
        LikeIcon.Glyph = _isLiked ? "\uE769" : "\uE768";
        LikeIcon.Foreground = _isLiked
            ? new SolidColorBrush(Microsoft.UI.Colors.Red)
            : (Brush)Resources["FazlakaTextSecondary"];

        try
        {
            var api = App.Services.Get<ApiService>();
            await api.ToggleLikeAsync("episode", ep.Id);
        }
        catch { }
    }

    private void Close()
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(HomePage));
        }
    }

    private void UpdateQueueLabel()
    {
        var queue = _audioPlayer.Queue;
        var current = Player.CurrentEpisode;
        if (queue.Count == 0 || current is null)
        {
            QueueLabel.Text = string.Empty;
            return;
        }

        var index = -1;
        for (var i = 0; i < queue.Count; i++)
        {
            if (queue[i].Id == current.Id)
            {
                index = i;
                break;
            }
        }

        QueueLabel.Text = index >= 0 ? $"الحلقة {index + 1} من {queue.Count}" : string.Empty;
    }
}
