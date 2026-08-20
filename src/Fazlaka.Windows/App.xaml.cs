using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Fazlaka.Windows.Services;
using Microsoft.UI.Xaml;
using Velopack;

namespace Fazlaka.Windows;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public static ServiceContainer Services { get; } = new();

    public App()
    {
        try
        {
            VelopackApp.Build().Run();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Fazlaka] Velopack startup skipped: {ex}");
        }

        InitializeComponent();

        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ConfigureServices();

        MainWindow = new MainWindow();
        MainWindow.Activate();
    }

    private static void ConfigureServices()
    {
        Services.Register(new SettingsService());
        Services.Register(() => new ApiService(Services.Get<SettingsService>()));
        Services.Register(() => new AuthService(Services.Get<ApiService>(), Services.Get<SettingsService>()));
        Services.Register(new AudioPlayerService());
        Services.Register(() => new UpdateService(Services.Get<ApiService>()));
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[Fazlaka] Unhandled UI exception: {e.Message}");
        if (e.Exception is not null)
        {
            Debug.WriteLine(e.Exception.ToString());
        }
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[Fazlaka] Fatal CLR exception: {e.ExceptionObject}");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Debug.WriteLine($"[Fazlaka] Unobserved task exception: {e.Exception}");
        e.SetObserved();
    }
}

public sealed class ServiceContainer
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, Func<object>> _factories = new();
    private readonly Dictionary<Type, object> _instances = new();

    public void Register<TService>(TService instance) where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        lock (_gate)
        {
            _factories[typeof(TService)] = () => instance;
            _instances[typeof(TService)] = instance;
        }
    }

    public void Register<TService>(Func<TService> factory) where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        lock (_gate)
        {
            _factories[typeof(TService)] = () => factory();
            _instances.Remove(typeof(TService));
        }
    }

    public TService Get<TService>() where TService : class
    {
        lock (_gate)
        {
            if (_instances.TryGetValue(typeof(TService), out var cached))
            {
                return (TService)cached;
            }

            if (!_factories.TryGetValue(typeof(TService), out var factory))
            {
                throw new InvalidOperationException(
                    $"No service of type '{typeof(TService).FullName}' has been registered.");
            }

            var instance = factory();
            _instances[typeof(TService)] = instance;
            return (TService)instance;
        }
    }

    public bool TryGet<TService>(out TService? service) where TService : class
    {
        lock (_gate)
        {
            if (_instances.TryGetValue(typeof(TService), out var cached))
            {
                service = (TService)cached;
                return true;
            }
        }

        service = null;
        return false;
    }

    public void Reset()
    {
        lock (_gate)
        {
            _factories.Clear();
            _instances.Clear();
        }
    }
}
