using System.Collections.ObjectModel;
using Wrsd.Core.Models;

namespace Wrsd.UI.ViewModels;

public sealed class ThreatItemViewModel : ObservableObject
{
    public required string KeyPath { get; init; }
    public required string RiskCategory { get; init; }
    public string MitreAttackId { get; init; } = "T1547.001";
    public string Severity { get; init; } = "High";
    public string SeverityColor { get; init; } = "#F43F5E";
    public DiffChangeType ChangeType { get; init; }
    public ObservableCollection<ValueDiffViewModel> Values { get; init; } = new();

    public static string ResolveMitreId(string? category)
    {
        if (category == null) return "T1547";
        if (category.Contains("Startup", StringComparison.OrdinalIgnoreCase) || category.Contains("Autorun", StringComparison.OrdinalIgnoreCase))
            return "T1547.001 — Boot/Logon Autostart: Registry Run Keys";
        if (category.Contains("Service", StringComparison.OrdinalIgnoreCase) || category.Contains("Driver", StringComparison.OrdinalIgnoreCase))
            return "T1543.003 — Create or Modify System Process: Windows Service";
        if (category.Contains("Debugger", StringComparison.OrdinalIgnoreCase) || category.Contains("IFEO", StringComparison.OrdinalIgnoreCase))
            return "T1546.012 — Event Triggered Execution: Image File Execution Options";
        if (category.Contains("Winlogon", StringComparison.OrdinalIgnoreCase) || category.Contains("Shell", StringComparison.OrdinalIgnoreCase))
            return "T1547.004 — Boot/Logon Autostart: Winlogon Helper DLL";
        return "T1112 — Modify Registry";
    }

    public static string ResolveSeverity(string? category)
    {
        if (category == null) return "Medium";
        if (category.Contains("Debugger", StringComparison.OrdinalIgnoreCase) || category.Contains("IFEO", StringComparison.OrdinalIgnoreCase))
            return "CRITICAL";
        if (category.Contains("Winlogon", StringComparison.OrdinalIgnoreCase))
            return "HIGH";
        if (category.Contains("Service", StringComparison.OrdinalIgnoreCase))
            return "HIGH";
        if (category.Contains("Startup", StringComparison.OrdinalIgnoreCase) || category.Contains("Autorun", StringComparison.OrdinalIgnoreCase))
            return "SUSPICIOUS";
        return "MEDIUM";
    }

    public static string ResolveSeverityColor(string severity)
    {
        return severity switch
        {
            "CRITICAL" => "#EF4444",
            "HIGH" => "#F43F5E",
            "SUSPICIOUS" => "#F59E0B",
            _ => "#8B5CF6"
        };
    }
}
