using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace NameTagGenerator.Output;

/// <summary>Finds Bambu Studio and opens files in it. It never closes or changes a running copy itself.</summary>
public static class BambuStudio
{
    const string ExeName = "bambu-studio.exe";

    public static string? FindExe()
    {
        foreach (var root in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData })
        {
            var candidate = Path.Combine(Environment.GetFolderPath(root), "Bambu Studio", ExeName);
            if (File.Exists(candidate)) return candidate;
        }

        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var uninstall = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall is null) continue;
            foreach (var name in uninstall.GetSubKeyNames())
            {
                using var app = uninstall.OpenSubKey(name);
                if (app?.GetValue("DisplayIcon") is string icon && icon.EndsWith(ExeName, StringComparison.OrdinalIgnoreCase) && File.Exists(icon))
                    return icon;
            }
        }
        return null;
    }

    public static bool IsRunning => Process.GetProcessesByName("bambu-studio").Length > 0;

    public static void Open(string exe, string file)
    {
        var start = new ProcessStartInfo(exe) { UseShellExecute = false };
        start.ArgumentList.Add(file);
        Process.Start(start)?.Dispose();
    }
}
