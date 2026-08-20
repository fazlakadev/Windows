using System;
using Fazlaka.Windows.Services;
using Fazlaka.Windows.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Fazlaka.Windows.Views;

public sealed partial class ProfilePage : Page
{
    private readonly SettingsService _settingsService;

    public ProfileViewModel ViewModel { get; }

    public string Channel { get; }

    public event EventHandler? LoggedOut;

    public ProfilePage()
    {
        _settingsService = App.Services.Get<SettingsService>();
        ViewModel = new ProfileViewModel();
        Channel = _settingsService.UpdateChannel;
        InitializeComponent();

        ViewModel.LoggedOut += OnViewModelLoggedOut;
        ViewModel.LoadProfileCommand.Execute(null);
    }

    private void OnViewModelLoggedOut(object? sender, EventArgs e)
    {
        LoggedOut?.Invoke(this, EventArgs.Empty);
    }
}
