using System.Diagnostics;
using System.Text;

namespace Fazlaka.Installer;

class Program
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

    static readonly string Purple = "\x1b[38;2;139;92;246m";
    static readonly string Gold = "\x1b[38;2;245;158;11m";
    static readonly string Green = "\x1b[38;2;16;185;129m";
    static readonly string Red = "\x1b[38;2;239;68;68m";
    static readonly string Dim = "\x1b[2m";
    static readonly string Bold = "\x1b[1m";
    static readonly string Reset = "\x1b[0m";

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "Fazlaka Installer - \u0641\u0630\u0644\u0629";
        Console.Clear();

        if (args.Contains("--uninstall"))
        {
            await Uninstall();
            return;
        }

        PrintHeader();

        Console.WriteLine();
        Console.WriteLine($"  {Dim}Welcome to Fazlaka Windows Installer{Reset}");
        Console.WriteLine($"  {Dim}  Version 1.1.0{Reset}");
        Console.WriteLine();
        Console.WriteLine($"  {Dim}This will install Fazlaka to:{Reset}");
        Console.WriteLine($"  {Purple}{InstallDir}{Reset}");
        Console.WriteLine();
        Console.WriteLine($"  {Dim}Press ENTER to install, or type 'q' to quit.{Reset}");
        Console.Write($"  {Purple}> {Reset}");
        var input = Console.ReadLine();
        if (input?.Trim().ToLower() == "q") return;

        Console.WriteLine();

        var appSource = FindAppSource();
        if (appSource == null)
        {
            PrintError("Could not find Fazlaka application files.");
            PrintError("Place setup.exe in the same folder as the published app.");
            WaitForExit();
            return;
        }

        try
        {
            Console.WriteLine($"  {Purple}{Bold}Installing Fazlaka...{Reset}");
            Console.WriteLine();

            await CopyFilesWithProgress(appSource);
            Console.WriteLine();
            Console.WriteLine($"  {Green}\u2713 Files copied{Reset}");

            CreateShortcuts();
            Console.WriteLine($"  {Green}\u2713 Shortcuts created{Reset}");

            RegisterUninstaller();
            Console.WriteLine($"  {Green}\u2713 Uninstaller registered{Reset}");

            Console.WriteLine();
            Console.WriteLine($"  {Green}{Bold}\u2713 Installation complete!{Reset}");
            Console.WriteLine();
            Console.Write($"  {Purple}Launch Fazlaka now? (Y/n): {Reset}");
            var launch = Console.ReadLine();
            if (string.IsNullOrEmpty(launch) || launch.Trim().ToLower() != "n")
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
            PrintError($"Installation failed: {ex.Message}");
        }

        WaitForExit();
    }

    static void PrintHeader()
    {
        Console.WriteLine();
        var lines = new[]
        {
            "\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588 \u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588 \u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588 \u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588 \u2588\u2588\u2588    \u2588\u2588\u2588 \u2588\u2588 \u2588\u2588\u2588    \u2588\u2588 \u2588\u2588\u2588  \u2588\u2588 \u2588\u2588      ",
            "\u2588\u2588      \u2588\u2588      \u2588\u2588      \u2588\u2588   \u2588\u2588 \u2588\u2588\u2588\u2588  \u2588\u2588\u2588\u2588 \u2588\u2588 \u2588\u2588\u2588\u2588   \u2588\u2588 \u2588\u2588   \u2588\u2588 \u2588\u2588 \u2588\u2588      ",
            "\u2588\u2588\u2588\u2588\u2588\u2588   \u2588\u2588\u2588\u2588\u2588\u2588   \u2588\u2588\u2588\u2588\u2588\u2588   \u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588  \u2588\u2588 \u2588\u2588 \u2588\u2588 \u2588\u2588 \u2588\u2588 \u2588\u2588\u2588\u2588  \u2588\u2588 \u2588\u2588\u2588\u2588\u2588\u2588 \u2588\u2588 \u2588\u2588      ",
            "\u2588\u2588      \u2588\u2588      \u2588\u2588      \u2588\u2588   \u2588\u2588 \u2588\u2588  \u2588\u2588  \u2588\u2588 \u2588\u2588 \u2588\u2588  \u2588\u2588 \u2588\u2588 \u2588\u2588   \u2588\u2588 \u2588\u2588   \u2588\u2588 \u2588\u2588 \u2588\u2588      ",
            "\u2588\u2588      \u2588\u2588      \u2588\u2588      \u2588\u2588   \u2588\u2588 \u2588\u2588      \u2588\u2588 \u2588\u2588 \u2588\u2588   \u2588\u2588\u2588\u2588 \u2588\u2588 \u2588\u2588   \u2588\u2588\u2588\u2588 \u2588\u2588 \u2588\u2588\u2588\u2588\u2588\u2588 "
        };

        foreach (var line in lines)
            Console.WriteLine($"  {Purple}{line}{Reset}");

        Console.WriteLine($"  {Gold}                              \u0641\u0630\u0644\u0629{Reset}");
        Console.WriteLine($"  {Dim}                           Windows Desktop App{Reset}");
        Console.WriteLine();
        Console.WriteLine($"  {Purple}\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550{Reset}");
    }

    static async Task CopyFilesWithProgress(string sourceDir)
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

            var pct = (double)current / total;
            var filled = (int)(pct * 40);
            Console.Write($"\r  {Purple}[{new string('\u2588', filled)}{Dim}{new string('\u2591', 40 - filled)}{Reset}{Purple}]{Reset} {Dim}{current}/{total}{Reset} {Gold}{pct:P0}{Reset}  ");
            await Task.Delay(5);
        }

        Console.Write($"\r  {Purple}[{new string('\u2588', 40)}]{Reset} {Green}{total}/{total} complete 100%{Reset}  ");
    }

    static void CreateShortcuts()
    {
        Directory.CreateDirectory(StartMenuPath);
        CreateLnk(Path.Combine(DesktopPath, "Fazlaka.lnk"),
            Path.Combine(InstallDir, "Fazlaka.exe"), "Fazlaka");
        CreateLnk(Path.Combine(StartMenuPath, "Fazlaka.lnk"),
            Path.Combine(InstallDir, "Fazlaka.exe"), "Fazlaka");
        CreateLnk(Path.Combine(StartMenuPath, "Uninstall Fazlaka.lnk"),
            Path.Combine(InstallDir, "FazlakaSetup.exe"), "Uninstall Fazlaka");
    }

    static void CreateLnk(string path, string target, string desc)
    {
        var ps = $"$w=New-Object -ComObject WScript.Shell;$s=$w.CreateShortcut('{path}');$s.TargetPath='{target}';$s.WorkingDirectory='{Path.GetDirectoryName(target)}';$s.Description='{desc}';$s.Save()";
        var psi = new ProcessStartInfo("powershell", $"-NoProfile -Command \"{ps}\"")
        {
            CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(psi)?.WaitForExit();
    }

    static void RegisterUninstaller()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Fazlaka");
            key.SetValue("DisplayName", "Fazlaka");
            key.SetValue("UninstallString", $"\"{Path.Combine(InstallDir, "FazlakaSetup.exe")}\" --uninstall");
            key.SetValue("InstallLocation", InstallDir);
            key.SetValue("DisplayVersion", "1.1.0");
            key.SetValue("Publisher", "Fazlaka");
            key.SetValue("DisplayIcon", $"\"{Path.Combine(InstallDir, "Fazlaka.exe")}\"");
        }
        catch { }
    }

    static async Task Uninstall()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Clear();
        Console.WriteLine();
        Console.WriteLine($"  {Red}{Bold}Uninstalling Fazlaka...{Reset}");
        Console.WriteLine();

        try
        {
            if (Directory.Exists(InstallDir))
            {
                Directory.Delete(InstallDir, true);
                Console.WriteLine($"  {Green}\u2713 Application files removed{Reset}");
            }

            var desktopLnk = Path.Combine(DesktopPath, "Fazlaka.lnk");
            if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

            if (Directory.Exists(StartMenuPath))
            {
                Directory.Delete(StartMenuPath, true);
                Console.WriteLine($"  {Green}\u2713 Start Menu shortcuts removed{Reset}");
            }

            try
            {
                Microsoft.Win32.Registry.LocalMachine.DeleteSubKeyTree(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Fazlaka", false);
                Console.WriteLine($"  {Green}\u2713 Registry entries removed{Reset}");
            }
            catch { }

            Console.WriteLine();
            Console.WriteLine($"  {Green}{Bold}\u2713 Fazlaka has been uninstalled.{Reset}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {Red}Uninstall error: {ex.Message}{Reset}");
        }

        Console.WriteLine();
        Console.Write("  Press any key to exit...");
        Console.ReadKey();
    }

    static string? FindAppSource()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "app"),
            Path.Combine(AppContext.BaseDirectory, "publish"),
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

    static void PrintError(string msg) => Console.WriteLine($"  \x1b[31m\u2717 {msg}{Reset}");
    static void PrintSuccess(string msg) => Console.WriteLine($"  {Green}\u2713 {msg}{Reset}");
    static void WaitForExit()
    {
        Console.WriteLine();
        Console.Write("  Press any key to exit...");
        Console.ReadKey();
    }
}
