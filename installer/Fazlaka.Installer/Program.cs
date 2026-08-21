using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace Fazlaka.Installer;

class Program
{
    const string AppName = "Fazlaka";

    static readonly string InstallDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
    static readonly string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    static readonly string StartMenuPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", AppName);

    static bool IsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    static void Elevate(string[] args)
    {
        var exe = Environment.ProcessPath ?? "";
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            Verb = "runas",
            Arguments = string.Join(" ", args)
        };
        try
        {
            Process.Start(psi);
        }
        catch
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  Administrator permission is required.");
            Console.ResetColor();
            Console.WriteLine("  Press any key to exit...");
            Console.ReadKey();
        }
        Environment.Exit(0);
    }

    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (!IsAdmin())
        {
            Elevate(args);
            return 0;
        }

        try
        {
            if (args.Contains("--uninstall"))
                return await Uninstall();

            return await Install();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine($"  FATAL ERROR: {ex.Message}");
            Console.WriteLine($"  {ex.StackTrace}");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("  Press any key to exit...");
            Console.ReadKey();
            return 1;
        }
    }

    static async Task<int> Install()
    {
        Console.Title = "Fazlaka Installer";
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine("  ╔══════════════════════════════════════════╗");
        Console.WriteLine("  ║          F A Z L A K A                   ║");
        Console.WriteLine("  ║          Windows Installer               ║");
        Console.WriteLine("  ║          Version 1.1.3                   ║");
        Console.WriteLine("  ╚══════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"  Install to: {InstallDir}");
        Console.WriteLine();
        Console.Write("  Press ENTER to install, or 'q' to quit: ");
        
        var input = Console.ReadLine();
        if (input?.Trim().ToLower() == "q") return 0;

        Console.WriteLine();

        var appSource = FindAppSource();
        if (appSource == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  ERROR: Could not find Fazlaka.exe");
            Console.WriteLine("  Make sure FazlakaSetup.exe is next to the 'app' folder.");
            Console.ResetColor();
            WaitForExit();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  Installing...");
        Console.ResetColor();
        Console.WriteLine();

        try
        {
            await CopyFiles(appSource);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ✓ Files copied");
            Console.ResetColor();

            CreateShortcuts();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ✓ Shortcuts created");
            Console.ResetColor();

            RegisterUninstaller();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ✓ Registered");
            Console.ResetColor();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ✓ Installation complete!");
            Console.ResetColor();
            Console.WriteLine();
            Console.Write("  Launch Fazlaka now? (Y/n): ");
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
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ Installation failed: {ex.Message}");
            Console.ResetColor();
        }

        WaitForExit();
        return 0;
    }

    static async Task<int> Uninstall()
    {
        Console.Title = "Fazlaka Uninstaller";
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine();
        Console.WriteLine("  Uninstalling Fazlaka...");
        Console.WriteLine();
        Console.ResetColor();

        try
        {
            if (Directory.Exists(InstallDir))
            {
                Directory.Delete(InstallDir, true);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✓ Files removed");
                Console.ResetColor();
            }

            var desktopLnk = Path.Combine(DesktopPath, "Fazlaka.lnk");
            if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

            if (Directory.Exists(StartMenuPath))
            {
                Directory.Delete(StartMenuPath, true);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✓ Shortcuts removed");
                Console.ResetColor();
            }

            try
            {
                Microsoft.Win32.Registry.LocalMachine.DeleteSubKeyTree(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Fazlaka", false);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✓ Registry cleaned");
                Console.ResetColor();
            }
            catch { }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ✓ Fazlaka has been uninstalled.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ Uninstall failed: {ex.Message}");
            Console.ResetColor();
        }

        WaitForExit();
        return 0;
    }

    static async Task CopyFiles(string sourceDir)
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
            var bar = new string('█', (int)(pct * 30));
            var empty = new string('░', 30 - (int)(pct * 30));
            Console.Write($"\r  [{bar}{empty}] {current}/{total} {pct:P0}  ");
            await Task.Delay(1);
        }
        Console.WriteLine();
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
            key.SetValue("DisplayVersion", "1.1.3");
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

    static void WaitForExit()
    {
        Console.WriteLine();
        Console.Write("  Press any key to exit...");
        Console.ReadKey();
    }
}
