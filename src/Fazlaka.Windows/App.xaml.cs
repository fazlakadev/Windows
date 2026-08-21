using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Fazlaka.Windows.Services;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Fazlaka.Windows;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public static ServiceContainer Services { get; } = new();

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Fazlaka", "crash.log");

    public static event EventHandler<DeepLinkAuthArgs>? DeepLinkAuthReceived;

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

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
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

            HandleProtocolActivation();
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

    private static void HandleProtocolActivation()
    {
        try
        {
            var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activatedArgs != null && activatedArgs.Kind == ExtendedActivationKind.Protocol)
            {
                var protocolData = activatedArgs.Data as global::Windows.ApplicationModel.Activation.ProtocolActivatedEventArgs;
                if (protocolData?.Uri != null)
                {
                    var uri = protocolData.Uri;
                    Log($"Protocol activated: {uri}");

                    if (uri.Scheme.Equals("fazlaka", StringComparison.OrdinalIgnoreCase) &&
                        uri.Host.Equals("auth", StringComparison.OrdinalIgnoreCase))
                    {
                        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                        var accessToken = query["accessToken"];
                        var refreshToken = query["refreshToken"];
                        var error = query["error"];

                        if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
                        {
                            Log($"Deep link auth success");
                            DeepLinkAuthReceived?.Invoke(null, new DeepLinkAuthArgs(accessToken, refreshToken));
                        }
                        else if (!string.IsNullOrEmpty(error))
                        {
                            Log($"Deep link auth error: {error}");
                            DeepLinkAuthReceived?.Invoke(null, new DeepLinkAuthArgs(error));
                        }
                        else
                        {
                            Log("Deep link auth: missing tokens and no error");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Protocol activation handling failed: {ex.Message}");
        }
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

public sealed class DeepLinkAuthArgs
{
    public string? AccessToken { get; }
    public string? RefreshToken { get; }
    public string? Error { get; }
    public bool IsSuccess => !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(RefreshToken);

    public DeepLinkAuthArgs(string accessToken, string refreshToken)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
    }

    public DeepLinkAuthArgs(string error)
    {
        Error = error;
    }
}
