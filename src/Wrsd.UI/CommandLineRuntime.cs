using System.Diagnostics;
using System.IO;

namespace Wrsd.UI;

/// <summary>
/// The command-line tool (wrsd-cli.exe) is a small framework-dependent program: it needs the shared .NET 10 runtime, which the
/// self-contained desktop app does not install. This helper detects it and offers a one-click install (D043).
/// </summary>
public static class CommandLineRuntime
{
    public const string DownloadUrl = "https://dotnet.microsoft.com/download/dotnet/10.0/runtime";
    public const string WingetId = "Microsoft.DotNet.Runtime.10";

    public static bool IsInstalled(IEnumerable<string>? roots = null)
    {
        foreach (var root in roots ?? Roots())
        {
            try
            {
                var shared = Path.Combine(root, "shared", "Microsoft.NETCore.App");
                if (Directory.Exists(shared) && Directory.GetDirectories(shared, "10.*").Length > 0) return true;
            }
            catch (IOException) { /* unreadable folder: treat as not installed */ }
            catch (UnauthorizedAccessException) { }
        }
        return false;
    }

    private static IEnumerable<string> Roots()
    {
        var env = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(env)) yield return env;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
    }

    /// <summary>Installs the runtime with the Windows Package Manager in a visible window (progress and any admin prompt are shown
    /// by Windows). Falls back to the download page when winget is missing. Returns true when the runtime is present afterwards.</summary>
    public static async Task<bool> InstallAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("winget",
                $"install --id {WingetId} -e --accept-source-agreements --accept-package-agreements") { UseShellExecute = true };
            using var p = Process.Start(psi);
            if (p != null) await p.WaitForExitAsync();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            OpenDownloadPage();
            return false;
        }
        return IsInstalled();
    }

    public static void OpenDownloadPage()
    {
        try { Process.Start(new ProcessStartInfo(DownloadUrl) { UseShellExecute = true }); } catch { /* no browser */ }
    }
}
