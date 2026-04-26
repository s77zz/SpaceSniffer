using System.Diagnostics;
using System.Security.Principal;

namespace SpaceSniffer.Services;

public static class ElevationService
{
    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RestartAsAdmin(string? path = null)
    {
        var args = string.IsNullOrEmpty(path) ? "" : $"--scan \"{path}\"";
        var process = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath,
            Arguments = args,
            Verb = "runas",
            UseShellExecute = true
        };

        try
        {
            Process.Start(process);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User cancelled UAC dialog — do nothing
            return;
        }

        Environment.Exit(0);
    }
}
