using System.Text;
using Wrsd.Core.Models;

namespace Wrsd.Core.Exporters;

public static class RegFileExporter
{
    public static string GenerateUndoReg(SnapshotDiffReport report, bool includeNoise = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();
        sb.AppendLine($"; WRSD Undo Script generated at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"; Baseline: {report.Baseline.CreatedAt:yyyy-MM-dd HH:mm:ss} | Target: {report.Target.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        var diffs = report.Filter(hideNoise: !includeNoise).ToList();

        // 1. Revert keys that were added by installer (delete them)
        // Group by descending path length so children are deleted before parents
        var addedKeys = diffs
            .Where(k => k.ChangeType == DiffChangeType.Added)
            .OrderByDescending(k => k.KeyPath.Length)
            .ToList();

        foreach (var key in addedKeys)
        {
            string fullRegKey = NormalizeToFullRegKey(key.KeyPath);
            sb.AppendLine($"[-{fullRegKey}]");
            sb.AppendLine();
        }

        // 2. Revert modified keys (restore baseline values or delete values added by installer)
        var modifiedKeys = diffs
            .Where(k => k.ChangeType == DiffChangeType.Modified)
            .ToList();

        foreach (var key in modifiedKeys)
        {
            string fullRegKey = NormalizeToFullRegKey(key.KeyPath);
            sb.AppendLine($"[{fullRegKey}]");

            foreach (var v in key.ValueDiffs)
            {
                if (v.ChangeType == DiffChangeType.Added)
                {
                    // Value was added -> in Undo we delete it: "Value"=-
                    sb.AppendLine(FormatDeleteValue(v.ValueName));
                }
                else if (v.ChangeType == DiffChangeType.Modified || v.ChangeType == DiffChangeType.Deleted)
                {
                    // Restore original value from baseline
                    if (v.Before != null)
                    {
                        sb.AppendLine(FormatRegValue(v.Before));
                    }
                }
            }
            sb.AppendLine();
        }

        // 3. Revert deleted keys (re-create them and their values from baseline)
        var deletedKeys = diffs
            .Where(k => k.ChangeType == DiffChangeType.Deleted)
            .OrderBy(k => k.KeyPath.Length)
            .ToList();

        foreach (var key in deletedKeys)
        {
            string fullRegKey = NormalizeToFullRegKey(key.KeyPath);
            sb.AppendLine($"[{fullRegKey}]");
            if (key.Before != null)
            {
                foreach (var v in key.Before.Values.Values)
                {
                    sb.AppendLine(FormatRegValue(v));
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string GenerateRedoReg(SnapshotDiffReport report, bool includeNoise = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();
        sb.AppendLine($"; WRSD Redo / Apply Script generated at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        var diffs = report.Filter(hideNoise: !includeNoise).ToList();

        // 1. Keys that were added or modified
        var targetKeys = diffs
            .Where(k => k.ChangeType == DiffChangeType.Added || k.ChangeType == DiffChangeType.Modified)
            .OrderBy(k => k.KeyPath.Length)
            .ToList();

        foreach (var key in targetKeys)
        {
            string fullRegKey = NormalizeToFullRegKey(key.KeyPath);
            sb.AppendLine($"[{fullRegKey}]");

            if (key.ChangeType == DiffChangeType.Added && key.After != null)
            {
                foreach (var v in key.After.Values.Values)
                {
                    sb.AppendLine(FormatRegValue(v));
                }
            }
            else if (key.ChangeType == DiffChangeType.Modified)
            {
                foreach (var v in key.ValueDiffs)
                {
                    if (v.ChangeType == DiffChangeType.Deleted)
                    {
                        sb.AppendLine(FormatDeleteValue(v.ValueName));
                    }
                    else if (v.After != null)
                    {
                        sb.AppendLine(FormatRegValue(v.After));
                    }
                }
            }

            sb.AppendLine();
        }

        // 2. Keys that were deleted in target
        var deletedKeys = diffs
            .Where(k => k.ChangeType == DiffChangeType.Deleted)
            .OrderByDescending(k => k.KeyPath.Length)
            .ToList();

        foreach (var key in deletedKeys)
        {
            string fullRegKey = NormalizeToFullRegKey(key.KeyPath);
            sb.AppendLine($"[-{fullRegKey}]");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string NormalizeToFullRegKey(string path)
    {
        if (path.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase))
            return "HKEY_LOCAL_MACHINE" + path[4..];
        if (path.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase))
            return "HKEY_CURRENT_USER" + path[4..];
        if (path.StartsWith("HKCR\\", StringComparison.OrdinalIgnoreCase))
            return "HKEY_CLASSES_ROOT" + path[4..];
        if (path.StartsWith("HKU\\", StringComparison.OrdinalIgnoreCase))
            return "HKEY_USERS" + path[3..];
        if (path.StartsWith("HKCC\\", StringComparison.OrdinalIgnoreCase))
            return "HKEY_CURRENT_CONFIG" + path[4..];

        return path;
    }

    private static string FormatDeleteValue(string name)
    {
        string escapedName = string.IsNullOrEmpty(name) ? "@" : $"\"{EscapeRegString(name)}\"";
        return $"{escapedName}=-";
    }

    private static string FormatRegValue(RegistryValueSnapshot v)
    {
        string namePart = string.IsNullOrEmpty(v.Name) ? "@" : $"\"{EscapeRegString(v.Name)}\"";

        switch (v.Kind)
        {
            case RegistryValueKind.String:
                string strVal = v.RawData.Length > 0 ? Encoding.Unicode.GetString(v.RawData).TrimEnd('\0') : "";
                return $"{namePart}=\"{EscapeRegString(strVal)}\"";

            case RegistryValueKind.DWord:
                uint dword = v.RawData.Length >= 4 ? BitConverter.ToUInt32(v.RawData, 0) : 0;
                return $"{namePart}=dword:{dword:x8}";

            case RegistryValueKind.QWord:
                ulong qword = v.RawData.Length >= 8 ? BitConverter.ToUInt64(v.RawData, 0) : 0;
                byte[] qBytes = BitConverter.GetBytes(qword);
                return $"{namePart}=hex(b):{FormatHexBytes(qBytes)}";

            case RegistryValueKind.ExpandString:
                return $"{namePart}=hex(2):{FormatHexBytes(v.RawData)}";

            case RegistryValueKind.MultiString:
                return $"{namePart}=hex(7):{FormatHexBytes(v.RawData)}";

            case RegistryValueKind.Binary:
            default:
                return $"{namePart}=hex:{FormatHexBytes(v.RawData)}";
        }
    }

    private static string FormatHexBytes(byte[] bytes)
    {
        if (bytes.Length == 0) return "";
        return string.Join(",", bytes.Select(b => $"{b:x2}"));
    }

    private static string EscapeRegString(string input)
    {
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
