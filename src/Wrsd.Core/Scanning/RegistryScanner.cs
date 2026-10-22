using System.Diagnostics;
using System.Security;
using Microsoft.Win32;
using Wrsd.Core.Models;

namespace Wrsd.Core.Scanning;

public sealed class RegistryScanner
{
    public static List<string> GetRootPathsForPreset(SnapshotScopePreset preset)
    {
        return preset switch
        {
            SnapshotScopePreset.QuickTriage => new List<string>
            {
                @"HKLM\SOFTWARE",
                @"HKCU\Software",
                @"HKLM\SYSTEM\CurrentControlSet\Services"
            },
            SnapshotScopePreset.SoftwareOnly => new List<string>
            {
                @"HKLM\SOFTWARE",
                @"HKCU\Software"
            },
            SnapshotScopePreset.SystemServices => new List<string>
            {
                @"HKLM\SYSTEM\CurrentControlSet\Services"
            },
            SnapshotScopePreset.PersistenceOnly => new List<string>
            {
                @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run",
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\RunOnce",
                @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon",
                @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options",
                @"HKLM\SYSTEM\CurrentControlSet\Services"
            },
            SnapshotScopePreset.FullSystem => new List<string>
            {
                @"HKLM",
                @"HKCU",
                @"HKCR",
                @"HKU",
                @"HKCC"
            },
            _ => new List<string> { @"HKLM\SOFTWARE", @"HKCU\Software" }
        };
    }

    public async Task<RegistrySnapshot> TakeSnapshotAsync(
        SnapshotScopePreset preset,
        List<string>? customRoots = null,
        IProgress<RegistryScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => TakeSnapshot(preset, customRoots, progress, cancellationToken), cancellationToken);
    }

    public RegistrySnapshot TakeSnapshot(
        SnapshotScopePreset preset,
        List<string>? customRoots = null,
        IProgress<RegistryScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var roots = customRoots ?? GetRootPathsForPreset(preset);
        var snapshot = new RegistrySnapshot
        {
            ScopePreset = preset,
            ScopeDescription = string.Join("; ", roots)
        };

        var sw = Stopwatch.StartNew();
        int keysScanned = 0;
        int valuesScanned = 0;
        var lastReportSw = Stopwatch.StartNew();

        foreach (var rootPath in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsed = ParseRoot(rootPath);
            if (parsed is null) continue;

            var (hive, subPath) = parsed.Value;

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                if (string.IsNullOrEmpty(subPath))
                {
                    ScanTree(baseKey, rootPath, snapshot, ref keysScanned, ref valuesScanned, progress, lastReportSw, sw, cancellationToken);
                }
                else
                {
                    using var subKey = baseKey.OpenSubKey(subPath, false);
                    if (subKey != null)
                    {
                        ScanTree(subKey, rootPath, snapshot, ref keysScanned, ref valuesScanned, progress, lastReportSw, sw, cancellationToken);
                    }
                }
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
            {
                // Access denied or unreadable root - ignore and continue
            }
        }

        sw.Stop();
        snapshot.Duration = sw.Elapsed;

        // Final progress report
        progress?.Report(new RegistryScanProgress
        {
            KeysScanned = keysScanned,
            ValuesScanned = valuesScanned,
            CurrentPath = "Done",
            Elapsed = sw.Elapsed
        });

        return snapshot;
    }

    private void ScanTree(
        RegistryKey key,
        string currentFullPath,
        RegistrySnapshot snapshot,
        ref int keysScanned,
        ref int valuesScanned,
        IProgress<RegistryScanProgress>? progress,
        Stopwatch lastReportSw,
        Stopwatch totalSw,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        keysScanned++;
        if (lastReportSw.ElapsedMilliseconds > 80 && progress != null)
        {
            lastReportSw.Restart();
            progress.Report(new RegistryScanProgress
            {
                KeysScanned = keysScanned,
                ValuesScanned = valuesScanned,
                CurrentPath = currentFullPath,
                Elapsed = totalSw.Elapsed
            });
        }

        string[] subKeyNames;
        try
        {
            subKeyNames = key.GetSubKeyNames();
        }
        catch
        {
            subKeyNames = Array.Empty<string>();
        }

        var keySnapshot = new RegistryKeySnapshot
        {
            Path = currentFullPath,
            SubKeyNames = subKeyNames.ToList()
        };

        // Enumerate values
        string[] valueNames;
        try
        {
            valueNames = key.GetValueNames();
        }
        catch
        {
            valueNames = Array.Empty<string>();
        }

        foreach (var valName in valueNames)
        {
            try
            {
                var kind = MapValueKind(key.GetValueKind(valName));
                var rawVal = key.GetValue(valName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                var valueSnapshot = RegistryValueSnapshot.Create(valName, kind, rawVal);
                keySnapshot.Values[valName] = valueSnapshot;
                valuesScanned++;
            }
            catch
            {
                // Value read error
            }
        }

        snapshot.Keys[currentFullPath] = keySnapshot;

        // Recursively enumerate subkeys
        foreach (var subName in subKeyNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var childKey = key.OpenSubKey(subName, false);
                if (childKey != null)
                {
                    string childPath = $"{currentFullPath}\\{subName}";
                    ScanTree(childKey, childPath, snapshot, ref keysScanned, ref valuesScanned, progress, lastReportSw, totalSw, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
            {
                // Restricted subkeys (e.g. SAM or protected security keys)
            }
        }
    }

    private static (RegistryHive Hive, string SubPath)? ParseRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return null;

        string normalized = root.Trim().Replace('/', '\\');
        int slashIdx = normalized.IndexOf('\\');
        string hivePart = slashIdx > 0 ? normalized[..slashIdx] : normalized;
        string subPath = slashIdx > 0 ? normalized[(slashIdx + 1)..] : string.Empty;

        RegistryHive hive = hivePart.ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => RegistryHive.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER" => RegistryHive.CurrentUser,
            "HKCR" or "HKEY_CLASSES_ROOT" => RegistryHive.ClassesRoot,
            "HKU" or "HKEY_USERS" => RegistryHive.Users,
            "HKCC" or "HKEY_CURRENT_CONFIG" => RegistryHive.CurrentConfig,
            _ => RegistryHive.LocalMachine
        };

        return (hive, subPath);
    }

    private static Wrsd.Core.Models.RegistryValueKind MapValueKind(Microsoft.Win32.RegistryValueKind kind) => kind switch
    {
        Microsoft.Win32.RegistryValueKind.String => Wrsd.Core.Models.RegistryValueKind.String,
        Microsoft.Win32.RegistryValueKind.ExpandString => Wrsd.Core.Models.RegistryValueKind.ExpandString,
        Microsoft.Win32.RegistryValueKind.Binary => Wrsd.Core.Models.RegistryValueKind.Binary,
        Microsoft.Win32.RegistryValueKind.DWord => Wrsd.Core.Models.RegistryValueKind.DWord,
        Microsoft.Win32.RegistryValueKind.MultiString => Wrsd.Core.Models.RegistryValueKind.MultiString,
        Microsoft.Win32.RegistryValueKind.QWord => Wrsd.Core.Models.RegistryValueKind.QWord,
        Microsoft.Win32.RegistryValueKind.None => Wrsd.Core.Models.RegistryValueKind.None,
        _ => Wrsd.Core.Models.RegistryValueKind.Unknown
    };
}
