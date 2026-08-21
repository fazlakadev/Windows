using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Fazlaka.Installer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--uninstall"))
        {
            Uninstall();
            Shutdown();
        }
    }

    private static void Uninstall()
    {
        var installDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fazlaka");

        if (Directory.Exists(installDir))
            Directory.Delete(installDir, true);

        var desktopLnk = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Fazlaka.lnk");
        if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

        var startMenu = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Fazlaka");
        if (Directory.Exists(startMenu)) Directory.Delete(startMenu, true);

        try
        {
            Registry.LocalMachine.DeleteSubKeyTree(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Fazlaka", false);
        }
        catch { }
    }
}
