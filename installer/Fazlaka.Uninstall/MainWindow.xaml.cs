using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Fazlaka.Uninstall;

public partial class MainWindow : Window
{
    private static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fazlaka");

    private string _installPath;

    public MainWindow()
    {
        InitializeComponent();
        _installPath = DetectInstallPath();
        InstallPathText.Text = _installPath;
    }

    private static string DetectInstallPath()
    {
        var exeDir = Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)) ?? string.Empty;

        if (File.Exists(Path.Combine(exeDir, "Fazlaka.exe")))
            return exeDir;

        if (Directory.Exists(DefaultPath) && File.Exists(Path.Combine(DefaultPath, "Fazlaka.exe")))
            return DefaultPath;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Fazlaka");
            var loc = key?.GetValue("InstallLocation") as string;
            if (!string.IsNullOrEmpty(loc) && Directory.Exists(loc))
                return loc;
        }
        catch { }

        return DefaultPath;
    }

    private async void OnUninstallClick(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to uninstall Fazlaka?\nAll files and settings will be permanently removed.",
            "Uninstall Fazlaka",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        ConfirmPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        ProgressBorder.Visibility = Visibility.Visible;
        UninstallButton.IsEnabled = false;

        try
        {
            DetailText.Text = "Removing application files...";
            await AnimateProgress(0.1);

            if (Directory.Exists(_installPath))
            {
                var files = Directory.GetFiles(_installPath, "*", SearchOption.AllDirectories);
                int count = 0;
                foreach (var file in files)
                {
                    try { File.Delete(file); } catch { }
                    count++;
                    if (count % 50 == 0)
                    {
                        DetailText.Text = $"Removed {count} / {files.Length} files";
                        await AnimateProgress(0.1 + 0.6 * ((double)count / files.Length));
                    }
                }
                try { Directory.Delete(_installPath, true); } catch { }
            }
            await AnimateProgress(0.7);

            DetailText.Text = "Removing shortcuts...";
            var desktopLnk = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Fazlaka.lnk");
            if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

            var startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs", "Fazlaka");
            if (Directory.Exists(startMenu)) Directory.Delete(startMenu, true);
            await AnimateProgress(0.85);

            DetailText.Text = "Removing registry entry...";
            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Fazlaka", false);
            }
            catch { }
            await AnimateProgress(0.95);

            await Task.Delay(300);

            ProgressPanel.Visibility = Visibility.Collapsed;
            ProgressBorder.Visibility = Visibility.Collapsed;
            DonePanel.Visibility = Visibility.Visible;

            UninstallButton.Visibility = Visibility.Collapsed;
            CloseButton.Visibility = Visibility.Collapsed;
            FinishButton.Visibility = Visibility.Visible;

            var appDir = Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            if (appDir != null && appDir.Equals(_installPath, StringComparison.OrdinalIgnoreCase))
            {
                ScheduleSelfDelete(_installPath);
            }
        }
        catch (Exception ex)
        {
            DetailText.Text = $"Error: {ex.Message}";
            UninstallButton.IsEnabled = true;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static void ScheduleSelfDelete(string installPath)
    {
        try
        {
            var batPath = Path.Combine(Path.GetTempPath(), "fazlaka_cleanup.bat");
            var batContent = $"@echo off\r\n" +
                           $"timeout /t 2 /nobreak > nul\r\n" +
                           $"rmdir /s /q \"{installPath}\"\r\n" +
                           $"del \"%~f0\"\r\n";
            File.WriteAllText(batPath, batContent);

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batPath}\"",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
        }
        catch { }
    }

    private async Task AnimateProgress(double target)
    {
        var current = ProgressBar.ActualWidth;
        var totalWidth = 408;
        var targetWidth = target * totalWidth;
        var steps = 12;
        var step = (targetWidth - current) / steps;

        for (int i = 0; i < steps; i++)
        {
            current += step;
            ProgressBar.Width = Math.Max(0, current);
            await Task.Delay(16);
        }
        ProgressBar.Width = Math.Max(0, targetWidth);
    }
}
