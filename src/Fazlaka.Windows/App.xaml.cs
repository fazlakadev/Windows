using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Fazlaka.Windows.Services;
using Microsoft.UI.Xaml;

namespace Fazlaka.Windows;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public static ServiceContainer Services { get; } = new();

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Fazlaka", "crash.log");

    public App()
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(LogPath, $"[{DateTime.Now}] App() ctor start\n");
        }
        catch { }

        try
        {
            InitializeComponent();
            Log("InitializeComponent done");
        }
        catch (Exception ex)
        {
            Log($"InitializeComponent FAILED: {ex}");
        }

        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Log("Exception handlers registered");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log("OnLaunched START");
        try
        {
            ConfigureServices();
            Log("Services configured");

            MainWindow = new MainWindow();
            Log("MainWindow created");

            MainWindow.Activate();
            Log("MainWindow activated");
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            Log($"OnLaunched FAILED: {inner.GetType().Name}: {inner.Message}");
            Log($"Full: {ex}");
        }
    }

    private static void ConfigureServices()
    {
        Services.Register(new SettingsService());
        Services.Register(() => new ApiService(Services.Get<SettingsService>()));
        Services.Register(() => new AuthService(Services.Get<ApiService>(), Services.Get<SettingsService>()));
        Services.Register(new AudioPlayerService());
        Services.Register(() => new UpdateService(Services.Get<ApiService>()));
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log($"Unhandled UI exception: {e.Message}");
        Debug.WriteLine($"[Fazlaka] Unhandled UI exception: {e.Message}");
        if (e.Exception is not null)
        {
            Log($"Exception: {e.Exception}");
            Debug.WriteLine(e.Exception.ToString());
        }
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        Log($"Fatal CLR exception: {e.ExceptionObject}");
        Debug.WriteLine($"[Fazlaka] Fatal CLR exception: {e.ExceptionObject}");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log($"Unobserved task exception: {e.Exception}");
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

            var instance = (TService)factory();
            _instances[typeof(TService)] = instance;
            return instance;
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
