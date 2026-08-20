using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Fazlaka.Installer;

public partial class MainWindow : Window
{
    const string AppName = "Fazlaka";
    const string InstallDirName = "Fazlaka";

    static readonly string InstallDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        InstallDirName);
    static readonly string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    static readonly string StartMenuPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        "Programs", AppName);

    bool _isInstalling;
    bool _isInstalled;

    public MainWindow()
    {
        InitializeComponent();
        InstallPathText.Text = InstallDir;
        _isInstalled = Directory.Exists(InstallDir) && File.Exists(Path.Combine(InstallDir, "Fazlaka.exe"));
        UpdateUI();
    }

    void UpdateUI()
    {
        if (_isInstalled)
        {
            ActionBtn.Content = "Reinstall";
            StatusText.Text = "Fazlaka is already installed. Click Reinstall to update.";
            UninstallBtn.Visibility = Visibility.Visible;
        }
        else
        {
            ActionBtn.Content = "Install";
            StatusText.Text = "Click Install to begin installation.";
            UninstallBtn.Visibility = Visibility.Collapsed;
        }
    }

    async void OnActionClick(object sender, RoutedEventArgs e)
    {
        if (_isInstalling) return;

        var appSource = FindAppSource();
        if (appSource == null)
        {
            ShowError("Could not find Fazlaka application files.\n\nMake sure FazlakaSetup.exe is in the same folder as the app/ directory.");
            return;
        }

        _isInstalling = true;
        ActionBtn.IsEnabled = false;
        UninstallBtn.IsEnabled = false;
        ActionBtn.Content = "Installing...";

        try
        {
            StatusText.Text = "Copying files...";
            await CopyFilesWithProgress(appSource);

            StatusText.Text = "Creating shortcuts...";
            ProgressBar.Value = 90;
            CreateShortcuts();

            StatusText.Text = "Registering...";
            ProgressBar.Value = 95;
            RegisterUninstaller();

            ProgressBar.Value = 100;
            StatusText.Text = "Installation complete!";
            _isInstalled = true;
            UpdateUI();
            ActionBtn.Content = "Launch";

            var result = MessageBox.Show(
                "Fazlaka has been installed successfully!\n\nWould you like to launch it now?",
                "Installation Complete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(InstallDir, "Fazlaka.exe"),
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            ShowError($"Installation failed: {ex.Message}");
        }
        finally
        {
            _isInstalling = false;
            ActionBtn.IsEnabled = true;
            UninstallBtn.IsEnabled = true;
            if (_isInstalled && ActionBtn.Content.ToString() != "Launch")
                UpdateUI();
        }
    }

    void OnUninstallClick(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to uninstall Fazlaka?",
            "Uninstall",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            if (Directory.Exists(InstallDir))
                Directory.Delete(InstallDir, true);

            var desktopLnk = Path.Combine(DesktopPath, "Fazlaka.lnk");
            if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

            if (Directory.Exists(StartMenuPath))
                Directory.Delete(StartMenuPath, true);

            try
            {
                Microsoft.Win32.Registry.LocalMachine.DeleteSubKeyTree(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Fazlaka", false);
            }
            catch { }

            _isInstalled = false;
            ProgressBar.Value = 0;
            UpdateUI();
            StatusText.Text = "Fazlaka has been uninstalled.";

            MessageBox.Show("Fazlaka has been uninstalled successfully.", "Uninstall Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError($"Uninstall failed: {ex.Message}");
        }
    }

    async Task CopyFilesWithProgress(string sourceDir)
    {
        Directory.CreateDirectory(InstallDir);
        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        int total = files.Length;
        int current = 0;

        foreach (var file in files)
        {
            current++;
            var rel = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(InstallDir, rel);
            var dir = Path.GetDirectoryName(dest);
            if (dir != null) Directory.CreateDirectory(dir);
            File.Copy(file, dest, true);

            var pct = (double)current / total * 85;
            await Dispatcher.InvokeAsync(() =>
            {
                ProgressBar.Value = pct;
                StatusText.Text = $"Copying files... ({current}/{total})";
            });
            await Task.Delay(5);
        }
    }

    void CreateShortcuts()
    {
        Directory.CreateDirectory(StartMenuPath);
        CreateLnk(Path.Combine(DesktopPath, "Fazlaka.lnk"),
            Path.Combine(InstallDir, "Fazlaka.exe"), "Fazlaka");
        CreateLnk(Path.Combine(StartMenuPath, "Fazlaka.lnk"),
            Path.Combine(InstallDir, "Fazlaka.exe"), "Fazlaka");
        CreateLnk(Path.Combine(StartMenuPath, "Uninstall Fazlaka.lnk"),
            Path.Combine(InstallDir, "FazlakaSetup.exe"), "Uninstall Fazlaka");
    }

    void CreateLnk(string path, string target, string desc)
    {
        var ps = $"$w=New-Object -ComObject WScript.Shell;$s=$w.CreateShortcut('{path}');$s.TargetPath='{target}';$s.WorkingDirectory='{Path.GetDirectoryName(target)}';$s.Description='{desc}';$s.Save()";
        var psi = new ProcessStartInfo("powershell", $"-NoProfile -Command \"{ps}\"")
        {
            CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(psi)?.WaitForExit();
    }

    void RegisterUninstaller()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Fazlaka");
            key.SetValue("DisplayName", "Fazlaka");
            key.SetValue("UninstallString", $"\"{Path.Combine(InstallDir, "FazlakaSetup.exe")}\" --uninstall");
            key.SetValue("InstallLocation", InstallDir);
            key.SetValue("DisplayVersion", "1.1.2");
            key.SetValue("Publisher", "Fazlaka");
            key.SetValue("DisplayIcon", $"\"{Path.Combine(InstallDir, "Fazlaka.exe")}\"");
        }
        catch { }
    }

    static string? FindAppSource()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "app"),
            AppContext.BaseDirectory,
            Path.GetDirectoryName(AppContext.BaseDirectory) ?? "",
        };
        foreach (var dir in candidates)
        {
            if (File.Exists(Path.Combine(dir, "Fazlaka.exe")))
                return dir;
        }
        return null;
    }

    void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }
}
