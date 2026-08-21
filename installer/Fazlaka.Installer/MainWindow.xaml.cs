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
    private int _currentStep = 1;
    private bool _installed;

    public MainWindow()
    {
        InitializeComponent();
        InstallPathText.Text = _installPath;
    }

    private void SetStep(int step)
    {
        _currentStep = step;

        WelcomePanel.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        LocationPanel.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        InstallingPanel.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        CompletePanel.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;

        BackButton.Visibility = step > 1 && step < 3 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Visibility = step < 3 || step == 4 ? Visibility.Visible : Visibility.Collapsed;
        CloseButton.Visibility = step < 3 || step == 4 ? Visibility.Visible : Visibility.Collapsed;
        UninstallButton.Visibility = _installed && step < 3 ? Visibility.Visible : Visibility.Collapsed;

        if (step == 1)
        {
            NextButton.Content = "Next \u2192";
        }
        else if (step == 2)
        {
            NextButton.Content = "Install \u2192";
        }
        else if (step == 4)
        {
            NextButton.Content = "Launch";
            CloseButton.Visibility = Visibility.Collapsed;
        }

        UpdateStepIndicator(step);
    }

    private void UpdateStepIndicator(int step)
    {
        var activeBrush = new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED));
        var inactiveBrush = new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51));

        Step1Dot.Fill = step >= 1 ? activeBrush : inactiveBrush;
        Step1Label.Foreground = step >= 1 ? activeBrush : inactiveBrush;

        Step2Dot.Fill = step >= 2 ? activeBrush : inactiveBrush;
        Step2Label.Foreground = step >= 2 ? activeBrush : inactiveBrush;

        Step3Dot.Fill = step >= 3 ? activeBrush : inactiveBrush;
        Step3Label.Foreground = step >= 3 ? activeBrush : inactiveBrush;

        Step4Dot.Fill = step >= 4 ? activeBrush : inactiveBrush;
        Step4Label.Foreground = step >= 4 ? activeBrush : inactiveBrush;
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

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 1)
        {
            SetStep(2);
        }
        else if (_currentStep == 2)
        {
            StartInstall();
        }
        else if (_currentStep == 4)
        {
            LaunchApp();
            Close();
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 2) SetStep(1);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnUninstallClick(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to uninstall Fazlaka?",
            "Uninstall Fazlaka",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        SetStep(3);
        NextButton.Visibility = Visibility.Collapsed;
        CloseButton.Visibility = Visibility.Collapsed;
        BackButton.Visibility = Visibility.Collapsed;

        try
        {
            ShowStatus("Removing installation...");

            UnregisterProtocol();

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
            DetailText.Text = "";
            await AnimateProgress(1.0);

            _installed = false;
            SetStep(4);
            CompletePanel.Visibility = Visibility.Collapsed;
            LocationPanel.Visibility = Visibility.Visible;
            SetStep(2);
        }
        catch (Exception ex)
        {
            ShowStatus($"Uninstall failed: {ex.Message}", true);
            NextButton.Visibility = Visibility.Visible;
            CloseButton.Visibility = Visibility.Visible;
        }
    }

    private async void StartInstall()
    {
        SetStep(3);
        NextButton.Visibility = Visibility.Collapsed;
        CloseButton.Visibility = Visibility.Collapsed;
        BackButton.Visibility = Visibility.Collapsed;

        try
        {
            var appSource = FindAppSource();
            if (appSource == null)
            {
                ShowStatus("Error: Could not find app files next to installer.", true);
                CloseButton.Visibility = Visibility.Visible;
                return;
            }

            ShowStatus("Creating installation directory...");
            DetailText.Text = _installPath;
            await AnimateProgress(0.03);
            Directory.CreateDirectory(_installPath);

            ShowStatus("Copying application files...");
            await CopyFilesWithProgress(appSource);

            ShowStatus("Creating shortcuts...");
            DetailText.Text = "Desktop and Start Menu";
            await AnimateProgress(0.92);
            CreateShortcuts();

            ShowStatus("Registering with Windows...");
            DetailText.Text = "Add/Remove Programs entry";
            await AnimateProgress(0.96);
            RegisterUninstaller();

            ShowStatus("Registering fazlaka:// protocol...");
            DetailText.Text = "Deep link protocol";
            await AnimateProgress(0.98);
            RegisterProtocol();

            ShowStatus("Installation complete!");
            DetailText.Text = "";
            await AnimateProgress(1.0);

            _installed = true;
            InstallPathFinal.Text = $"Installed to: {_installPath}";
            await Task.Delay(400);
            SetStep(4);
        }
        catch (Exception ex)
        {
            ShowStatus($"Installation failed: {ex.Message}", true);
            DetailText.Text = ex.ToString();
            CloseButton.Visibility = Visibility.Visible;
        }
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

            if (current % 20 == 0 || current == total)
            {
                DetailText.Text = $"{current} / {total} files";
            }

            var pct = (double)current / total;
            await AnimateProgress(0.03 + pct * 0.87);

            if (current % 5 == 0)
                await Task.Delay(1);
        }
    }

    private async Task AnimateProgress(double target)
    {
        var current = ProgressBar.ActualWidth;
        var totalWidth = 616;
        var targetWidth = target * totalWidth;
        var steps = 15;
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
            Path.Combine(_installPath, "FazlakaUninstall.exe"));
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

    private void RegisterProtocol()
    {
        try
        {
            var exe = Path.Combine(_installPath, "Fazlaka.exe");

            using var root = Registry.ClassesRoot.CreateSubKey("fazlaka");
            root.SetValue("", "URL:Fazlaka Protocol");
            root.SetValue("URL Protocol", "");

            using var shell = root.CreateSubKey("shell\\open\\command");
            shell.SetValue("", $"\"{exe}\" \"%1\"");
        }
        catch { }
    }

    private void UnregisterProtocol()
    {
        try
        {
            Registry.ClassesRoot.DeleteSubKeyTree("fazlaka", false);
        }
        catch { }
    }

    private void RegisterUninstaller()
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Fazlaka");
            key.SetValue("DisplayName", "Fazlaka");
            key.SetValue("UninstallString", $"\"{Path.Combine(_installPath, "FazlakaUninstall.exe")}\"");
            key.SetValue("InstallLocation", _installPath);
            key.SetValue("DisplayVersion", "1.1.8");
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

    private static string? FindUninstallSource(string appSource)
    {
        var exe = Path.Combine(appSource, "FazlakaUninstall.exe");
        if (File.Exists(exe)) return exe;
        return null;
    }

    private void LaunchApp()
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
    }
}
