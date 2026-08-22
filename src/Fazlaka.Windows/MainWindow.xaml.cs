using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Fazlaka.Windows.Services;
using Fazlaka.Windows.ViewModels;
using Fazlaka.Windows.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.Graphics;
using Colors = Microsoft.UI.Colors;

namespace Fazlaka.Windows;

public sealed partial class MainWindow : Window
{
    private static readonly Dictionary<string, Type> Routes = new()
    {
        ["home"] = typeof(HomePage),
        ["search"] = typeof(SearchPage),
        ["seasons"] = typeof(SeasonsPage),
        ["playlists"] = typeof(PlaylistsPage),
        ["articles"] = typeof(ArticlesPage),
        ["history"] = typeof(HistoryPage),
        ["likes"] = typeof(LikesPage),
        ["profile"] = typeof(ProfilePage),
    };

    private static readonly Dictionary<Type, string> RouteKeys = new()
    {
        [typeof(HomePage)] = "home",
        [typeof(SearchPage)] = "search",
        [typeof(SeasonsPage)] = "seasons",
        [typeof(PlaylistsPage)] = "playlists",
        [typeof(ArticlesPage)] = "articles",
        [typeof(HistoryPage)] = "history",
        [typeof(LikesPage)] = "likes",
        [typeof(ProfilePage)] = "profile",
    };

    private readonly bool _initialized;
    private bool _syncingSelection;
    private ProfilePage? _hookedProfilePage;

    public static MainWindow? Instance { get; private set; }

    public MainViewModel ViewModel { get; }

    public PlayerViewModel Player { get; }

    public static PlayerViewModel SharedPlayer => ((MainWindow)App.MainWindow!).Player;

    public MainWindow()
    {
        Instance = this;
        ViewModel = new MainViewModel();
        Player = new PlayerViewModel();
        ViewModel.CheckLoginState();

        InitializeComponent();
        _initialized = true;

        Title = "فذلكة";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        ConfigureWindowChrome();

        NavView.SelectionChanged += OnNavViewSelectionChanged;
        NavView.BackRequested += OnNavViewBackRequested;
        ContentFrame.Navigated += OnContentFrameNavigated;
        Player.PropertyChanged += OnPlayerPropertyChanged;
        ViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
        LoginRoot.LoginSucceeded += OnLoginSucceeded;
        App.DeepLinkAuthReceived += OnDeepLinkAuthReceived;

        ShowShell(ViewModel.IsLoggedIn);
        InitNetworkMonitor();
        ShowLockIfNeeded();
    }

    private void ConfigureWindowChrome()
    {
        try
        {
            var appWindow = AppWindow;
            appWindow.Resize(new SizeInt32(1220, 800));

            var titleBar = appWindow.TitleBar;
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonHoverForegroundColor = Colors.White;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor = global::Windows.UI.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF);
            titleBar.ButtonPressedBackgroundColor = global::Windows.UI.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = 980;
                presenter.PreferredMinimumHeight = 640;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Fazlaka] Window chrome customization skipped: {ex.Message}");
        }
    }

    private void ShowShell(bool signedIn)
    {
        ShellRoot.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
        LoginRoot.Visibility = signedIn ? Visibility.Collapsed : Visibility.Visible;

        if (signedIn)
        {
            NavigateToRoute("home");
            ViewModel.CheckForUpdatesCommand.Execute(null);
        }
        else
        {
            UnhookProfilePage();
            ContentFrame.BackStack.Clear();
            ContentFrame.ForwardStack.Clear();
            ContentFrame.Content = null;
            NavView.SelectedItem = null;
        }

        UpdateChrome();
    }

    private void NavigateToRoute(string route)
    {
        if (!_initialized || !Routes.TryGetValue(route, out var pageType))
        {
            return;
        }

        if (ContentFrame.Content?.GetType() == pageType)
        {
            SyncSelection(pageType);
            return;
        }

        ContentFrame.Navigate(pageType);
    }

    private void OnNavViewSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_syncingSelection)
        {
            return;
        }

        if (args.SelectedItemContainer?.Tag is string route)
        {
            NavigateToRoute(route);
        }
    }

    private void OnNavViewBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
        }
    }

    private void OnContentFrameNavigated(object sender, NavigationEventArgs e)
    {
        SyncSelection(ContentFrame.Content?.GetType());
        NavView.IsBackEnabled = ContentFrame.CanGoBack;
        HookProfilePage();
        UpdateChrome();
    }

    private void SyncSelection(Type? pageType)
    {
        if (pageType is null || !RouteKeys.TryGetValue(pageType, out var route))
        {
            SelectNavItem(null);
            return;
        }

        SelectNavItem(FindNavItem(route));
    }

    private NavigationViewItem? FindNavItem(string route)
    {
        foreach (var item in NavView.MenuItems)
        {
            if (item is NavigationViewItem nvi && nvi.Tag as string == route)
            {
                return nvi;
            }
        }

        foreach (var item in NavView.FooterMenuItems)
        {
            if (item is NavigationViewItem nvi && nvi.Tag as string == route)
            {
                return nvi;
            }
        }

        return null;
    }

    private void SelectNavItem(NavigationViewItem? item)
    {
        _syncingSelection = true;
        try
        {
            NavView.SelectedItem = item;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void HookProfilePage()
    {
        if (ReferenceEquals(_hookedProfilePage, ContentFrame.Content))
        {
            return;
        }

        UnhookProfilePage();

        if (ContentFrame.Content is ProfilePage profile)
        {
            _hookedProfilePage = profile;
            profile.LoggedOut += OnProfileLoggedOut;
        }
    }

    private void UnhookProfilePage()
    {
        if (_hookedProfilePage is not null)
        {
            _hookedProfilePage.LoggedOut -= OnProfileLoggedOut;
            _hookedProfilePage = null;
        }
    }

    private void OnProfileLoggedOut(object? sender, EventArgs e)
    {
        App.Services.Get<AudioPlayerService>().Pause();
        ViewModel.CheckLoginState();
        ShowShell(false);
    }

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        ViewModel.CheckLoginState();
        ShowShell(true);
    }

    private void OnDeepLinkAuthReceived(object? sender, DeepLinkAuthArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                if (e.IsSuccess)
                {
                    var settings = App.Services.Get<SettingsService>();
                    var api = App.Services.Get<ApiService>();
                    settings.AuthToken = e.AccessToken;
                    settings.RefreshToken = e.RefreshToken;
                    api.SetAuthToken(e.AccessToken);
                    ViewModel.CheckLoginState();
                    ShowShell(true);
                }
                else
                {
                    LoginRoot.ShowError($"فشل تسجيل الدخول: {e.Error}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Fazlaka] Deep link auth handling failed: {ex.Message}");
            }
        });
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerViewModel.HasEpisode) or nameof(PlayerViewModel.CurrentEpisode))
        {
            UpdateChrome();
        }
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.HasUpdate) or nameof(MainViewModel.IsLoggedIn))
        {
            UpdateChrome();
        }
    }

    private void UpdateChrome()
    {
        if (!_initialized)
        {
            return;
        }

        var showMini = ViewModel.IsLoggedIn
                       && Player.HasEpisode
                       && ContentFrame.Content is not PlayerPage;

        MiniPlayer.Visibility = showMini ? Visibility.Visible : Visibility.Collapsed;
        UpdateNavItem.Visibility = ViewModel.HasUpdate ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnMiniPlayerTapped(object sender, TappedRoutedEventArgs e)
    {
        ExpandPlayer();
    }

    private void OnExpandPlayerClicked(object sender, RoutedEventArgs e)
    {
        ExpandPlayer();
    }

    private void ExpandPlayer()
    {
        if (ContentFrame.Content is not PlayerPage)
        {
            ContentFrame.Navigate(typeof(PlayerPage));
        }
    }

    public void NavigateToSeason(string? seasonId)
    {
        NavigateToRoute("seasons");
        if (ContentFrame.Content is SeasonsPage seasonsPage)
        {
            seasonsPage.SelectSeasonById(seasonId);
        }
    }

    public void NavigateToSubPage(Type pageType)
    {
        if (_initialized)
        {
            ContentFrame.Navigate(pageType);
        }
    }

    private void InitNetworkMonitor()
    {
        try
        {
            var monitor = App.Services.Get<Services.NetworkMonitorService>();
            UpdateNetworkStatus(monitor.IsConnected);
            monitor.ConnectivityChanged += (_, connected) =>
            {
                DispatcherQueue.TryEnqueue(() => UpdateNetworkStatus(connected));
            };
        }
        catch { }
    }

    private void UpdateNetworkStatus(bool connected)
    {
        NetworkDot.Fill = connected
            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 34, 197, 94))
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 239, 68, 68));
        NetworkLabel.Text = connected ? "متصل" : "غير متصل";
    }

    private void ShowLockIfNeeded()
    {
        try
        {
            var security = App.Services.Get<Services.SecurityService>();
            if (ViewModel.IsLoggedIn && security.ShouldLockOnStart())
            {
                ShellRoot.Visibility = Visibility.Collapsed;
                LockRoot.Visibility = Visibility.Visible;
                LockRoot.Unlocked += OnLockUnlocked;
            }
        }
        catch { }
    }

    private void OnLockUnlocked(object? sender, EventArgs e)
    {
        LockRoot.Unlocked -= OnLockUnlocked;
        LockRoot.Visibility = Visibility.Collapsed;
        ShellRoot.Visibility = Visibility.Visible;
    }
}
