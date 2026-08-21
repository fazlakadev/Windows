using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Fazlaka.Installer;

public partial class MainWindow : Window
{
    private static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fazlaka");

    private string _installPath = DefaultPath;
    private bool _installed;

    public MainWindow()
    {
        InitializeComponent();
        InstallPathText.Text = _installPath;
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Install Location",
            FolderName = _installPath
        };

        if (dialog.ShowDialog(this) == true)
        {
            _installPath = Path.Combine(dialog.FolderName, "Fazlaka");
            InstallPathText.Text = _installPath;
        }
    }

    private async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        CancelButton.IsEnabled = false;

        try
        {
            var appSource = FindAppSource();
            if (appSource == null)
            {
                ShowStatus("Error: Could not find app files next to installer.", true);
                InstallButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
                return;
            }

            ShowStatus("Creating installation directory...");
            await AnimateProgress(0.05);
            Directory.CreateDirectory(_installPath);

            ShowStatus("Copying files...");
            await CopyFilesWithProgress(appSource);

            ShowStatus("Creating shortcuts...");
            await AnimateProgress(0.92);
            CreateShortcuts();

            ShowStatus("Registering...");
            await AnimateProgress(0.96);
            RegisterUninstaller();

            ShowStatus("Installation complete!");
            await AnimateProgress(1.0);

            _installed = true;
            InstallButton.Visibility = Visibility.Collapsed;
            UninstallButton.Visibility = Visibility.Visible;
            CancelButton.Content = "Launch";
        }
        catch (Exception ex)
        {
            ShowStatus($"Installation failed: {ex.Message}", true);
            InstallButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
        }
    }

    private async void OnUninstallClick(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to uninstall Fazlaka?",
            "Uninstall Fazlaka",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        UninstallButton.IsEnabled = false;

        try
        {
            ShowStatus("Uninstalling...");

            if (Directory.Exists(_installPath))
                Directory.Delete(_installPath, true);

            var desktopLnk = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Fazlaka.lnk");
            if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

            var startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs", "Fazlaka");
            if (Directory.Exists(startMenu)) Directory.Delete(startMenu, true);

            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Fazlaka", false);
            }
            catch { }

            ShowStatus("Fazlaka has been uninstalled.");
            await AnimateProgress(1.0);

            _installed = false;
            InstallButton.Visibility = Visibility.Visible;
            InstallButton.IsEnabled = true;
            UninstallButton.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowStatus($"Uninstall failed: {ex.Message}", true);
            UninstallButton.IsEnabled = true;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (_installed)
        {
            var exe = Path.Combine(_installPath, "Fazlaka.exe");
            if (File.Exists(exe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true
                });
            }
        }
        Close();
    }

    private void ShowStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError
            ? new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44))
            : new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
    }

    private async Task CopyFilesWithProgress(string sourceDir)
    {
        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        int total = files.Length;
        int current = 0;

        foreach (var file in files)
        {
            current++;
            var rel = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(_installPath, rel);
            var dir = Path.GetDirectoryName(dest);
            if (dir != null) Directory.CreateDirectory(dir);
            File.Copy(file, dest, true);

            var pct = (double)current / total;
            ShowStatus($"Copying files... {current}/{total}");
            await AnimateProgress(0.05 + pct * 0.85);
        }
    }

    private async Task AnimateProgress(double target)
    {
        var current = ProgressBar.ActualWidth;
        var targetWidth = target * 440;
        var steps = 20;
        var step = (targetWidth - current) / steps;

        for (int i = 0; i < steps; i++)
        {
            current += step;
            ProgressBar.Width = Math.Max(0, current);
            await Task.Delay(16);
        }
        ProgressBar.Width = Math.Max(0, targetWidth);
    }

    private void CreateShortcuts()
    {
        var startMenuPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs", "Fazlaka");
        Directory.CreateDirectory(startMenuPath);

        CreateLnk(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Fazlaka.lnk"),
            Path.Combine(_installPath, "Fazlaka.exe"));

        CreateLnk(
            Path.Combine(startMenuPath, "Fazlaka.lnk"),
            Path.Combine(_installPath, "Fazlaka.exe"));

        CreateLnk(
            Path.Combine(startMenuPath, "Uninstall Fazlaka.lnk"),
            Path.Combine(_installPath, "FazlakaSetup.exe"));
    }

    private static void CreateLnk(string path, string target)
    {
        var ps = $"$w=New-Object -ComObject WScript.Shell;" +
                 $"$s=$w.CreateShortcut('{path}');" +
                 $"$s.TargetPath='{target}';" +
                 $"$s.WorkingDirectory='{Path.GetDirectoryName(target)}';" +
                 $"$s.Description='Fazlaka';" +
                 $"$s.Save()";
        var psi = new ProcessStartInfo("powershell", $"-NoProfile -Command \"{ps}\"")
        {
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(psi)?.WaitForExit();
    }

    private void RegisterUninstaller()
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Fazlaka");
            key.SetValue("DisplayName", "Fazlaka");
            key.SetValue("UninstallString", $"\"{Path.Combine(_installPath, "FazlakaSetup.exe")}\" --uninstall");
            key.SetValue("InstallLocation", _installPath);
            key.SetValue("DisplayVersion", "1.1.4");
            key.SetValue("Publisher", "Fazlaka");
            key.SetValue("DisplayIcon", $"\"{Path.Combine(_installPath, "Fazlaka.exe")}\"");
        }
        catch { }
    }

    private static string? FindAppSource()
    {
        var baseDir = AppContext.BaseDirectory;

        var candidates = new[]
        {
            Path.Combine(baseDir, "app"),
            baseDir
        };

        foreach (var dir in candidates)
        {
            if (File.Exists(Path.Combine(dir, "Fazlaka.exe")))
                return dir;
        }
        return null;
    }
}
