using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Fazlaka.Windows.Services;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Win32;

namespace Fazlaka.Windows;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public static ServiceContainer Services { get; } = new();

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Fazlaka", "debug.log");

    private static readonly string PendingAuthPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Fazlaka", "pending-auth.txt");

    private static AppInstance? _mainInstance;
    private static Mutex? _mutex;
    private static FileSystemWatcher? _authWatcher;

    public static event EventHandler<DeepLinkAuthArgs>? DeepLinkAuthReceived;

    public App()
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (dir != null) Directory.CreateDirectory(dir);
            var pid = Environment.ProcessId;
            File.AppendAllText(LogPath, $"\n=== New instance PID={pid} [{DateTime.Now}] ===\n");
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
            bool createdNew;
            _mutex = new Mutex(true, "Global\\FazlakaDesktopApp", out createdNew);

            if (!createdNew)
            {
                Log("Another instance is running — forwarding deep link");
                var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                Log($"Activation kind: {activatedArgs.Kind}");

                Uri? deepLinkUri = null;

                if (activatedArgs.Kind == ExtendedActivationKind.Protocol)
                {
                    var proto = activatedArgs.Data as global::Windows.ApplicationModel.Activation.ProtocolActivatedEventArgs;
                    Log($"Protocol URI: {proto?.Uri}");
                    deepLinkUri = proto?.Uri;
                }

                if (deepLinkUri == null)
                {
                    var cliArgs = Environment.GetCommandLineArgs();
                    foreach (var a in cliArgs)
                    {
                        Log($"CLI arg: {a}");
                        if (a.StartsWith("fazlaka://", StringComparison.OrdinalIgnoreCase))
                        {
                            deepLinkUri = new Uri(a);
                            break;
                        }
                    }
                }

                if (deepLinkUri != null &&
                    deepLinkUri.Scheme.Equals("fazlaka", StringComparison.OrdinalIgnoreCase) &&
                    deepLinkUri.Host.Equals("auth", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.WriteAllText(PendingAuthPath, deepLinkUri.ToString());
                        Log($"Written to {PendingAuthPath}: {deepLinkUri}");
                    }
                    catch (Exception ex) { Log($"Failed to write pending auth: {ex.Message}"); }
                }
                else
                {
                    Log($"No valid deep link found (uri={deepLinkUri})");
                }

                Environment.Exit(0);
                return;
            }

            _mainInstance = AppInstance.FindOrRegisterForKey("main");

            ConfigureServices();
            Log("Services configured");

            MainWindow = new MainWindow();
            Log("MainWindow created");

            MainWindow.Activate();
            Log("MainWindow activated");

            RegisterProtocol();
            StartAuthWatcher();

            DeletePendingAuthFile();

            var launchArgs = _mainInstance.GetActivatedEventArgs();
            Log($"Launch activation kind: {launchArgs.Kind}");

            Uri? initialDeepLink = null;

            if (launchArgs.Kind == ExtendedActivationKind.Protocol)
            {
                var proto = launchArgs.Data as global::Windows.ApplicationModel.Activation.ProtocolActivatedEventArgs;
                initialDeepLink = proto?.Uri;
            }

            if (initialDeepLink == null)
            {
                var cliArgs = Environment.GetCommandLineArgs();
                foreach (var arg in cliArgs)
                {
                    Log($"CLI arg: {arg}");
                    if (arg.StartsWith("fazlaka://", StringComparison.OrdinalIgnoreCase))
                    {
                        initialDeepLink = new Uri(arg);
                        break;
                    }
                }
            }

            if (initialDeepLink != null &&
                initialDeepLink.Scheme.Equals("fazlaka", StringComparison.OrdinalIgnoreCase) &&
                initialDeepLink.Host.Equals("auth", StringComparison.OrdinalIgnoreCase))
            {
                Log($"App launched via deep link: {initialDeepLink}");
                ProcessDeepLinkUri(initialDeepLink);
            }
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
        Services.Register(() => new ViewModels.PlaylistsViewModel());
        Services.Register(() => new ViewModels.ArticlesViewModel());
        Services.Register(new NetworkMonitorService());
        Services.Register(() => new SecurityService(Services.Get<SettingsService>()));
    }

    private static void HandleProtocolActivation(AppActivationArguments? activatedArgs = null)
    {
        try
        {
            var args = activatedArgs ?? _mainInstance?.GetActivatedEventArgs();
            if (args == null || args.Kind != ExtendedActivationKind.Protocol) return;

            var protocolData = args.Data as global::Windows.ApplicationModel.Activation.ProtocolActivatedEventArgs;
            if (protocolData?.Uri == null) return;

            var uri = protocolData.Uri;
            Log($"Protocol activated: {uri}");

            if (uri.Scheme.Equals("fazlaka", StringComparison.OrdinalIgnoreCase) &&
                uri.Host.Equals("auth", StringComparison.OrdinalIgnoreCase))
            {
                ProcessDeepLinkUri(uri);
            }
        }
        catch (Exception ex)
        {
            Log($"Protocol activation handling failed: {ex.Message}");
        }
    }

    private static void ProcessDeepLinkUri(Uri uri)
    {
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var accessToken = query["accessToken"];
        var refreshToken = query["refreshToken"];
        var error = query["error"];

        if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
        {
            Log("Deep link auth success");
            MainWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                DeepLinkAuthReceived?.Invoke(null, new DeepLinkAuthArgs(accessToken, refreshToken));
            });
        }
        else if (!string.IsNullOrEmpty(error))
        {
            Log($"Deep link auth error: {error}");
            MainWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                DeepLinkAuthReceived?.Invoke(null, new DeepLinkAuthArgs(error));
            });
        }
        else
        {
            Log("Deep link auth: missing tokens and no error");
        }
    }

    private static void StartAuthWatcher()
    {
        try
        {
            var dir = Path.GetDirectoryName(PendingAuthPath);
            if (dir == null) return;
            Directory.CreateDirectory(dir);

            DeletePendingAuthFile();

            _authWatcher = new FileSystemWatcher(dir, "pending-auth.txt");
            _authWatcher.Created += OnPendingAuthFile;
            _authWatcher.Changed += OnPendingAuthFile;
            _authWatcher.EnableRaisingEvents = true;
            Log("Auth file watcher started");
        }
        catch (Exception ex)
        {
            Log($"Auth watcher failed: {ex.Message}");
        }
    }

    private static void OnPendingAuthFile(object sender, FileSystemEventArgs e)
    {
        try
        {
            Log($"File watcher triggered: {e.ChangeType} {e.FullPath}");
            Thread.Sleep(300);
            if (!File.Exists(PendingAuthPath))
            {
                Log("pending-auth.txt not found after trigger");
                return;
            }

            var uriString = File.ReadAllText(PendingAuthPath).Trim();
            Log($"Read URI: {uriString}");
            try { File.Delete(PendingAuthPath); } catch { }

            if (string.IsNullOrEmpty(uriString))
            {
                Log("URI is empty");
                return;
            }

            var uri = new Uri(uriString);
            if (uri.Scheme.Equals("fazlaka", StringComparison.OrdinalIgnoreCase) &&
                uri.Host.Equals("auth", StringComparison.OrdinalIgnoreCase))
            {
                Log("Calling ProcessDeepLinkUri from watcher");
                ProcessDeepLinkUri(uri);
            }
            else
            {
                Log($"Wrong scheme/host: {uri.Scheme}/{uri.Host}");
            }
        }
        catch (Exception ex)
        {
            Log($"Pending auth file processing FAILED: {ex}");
        }
    }

    private static void DeletePendingAuthFile()
    {
        try { if (File.Exists(PendingAuthPath)) File.Delete(PendingAuthPath); } catch { }
    }

    private static void RegisterProtocol()
    {
        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe)) return;

            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\fazlaka");
            key.SetValue("", "URL:Fazlaka Protocol");
            key.SetValue("URL Protocol", "");

            using var shell = key.CreateSubKey("shell\\open\\command");
            shell.SetValue("", $"\"{exe}\" \"%1\"");
        }
        catch (Exception ex) { Log($"RegisterProtocol failed: {ex.Message}"); }
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
