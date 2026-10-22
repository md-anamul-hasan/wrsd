using System.Text;
using Wrsd.Core.Models;

namespace Wrsd.Core.Exporters;

public static class CsvDiffExporter
{
    public static string ExportToCsv(SnapshotDiffReport report)
    {
        var sb = new StringBuilder();
        // CSV Header
        sb.AppendLine("KeyPath,ValueName,ChangeType,ValueType,IsPersistenceRisk,RiskCategory,BeforeData,AfterData");

        foreach (var key in report.AllKeyDiffs)
        {
            if (key.ChangeType == DiffChangeType.Added && key.ValueDiffs.Count == 0)
            {
                sb.AppendLine($"{Escape(key.KeyPath)},,Added,,{key.IsPersistenceRisk},{Escape(key.RiskCategory)},,");
                continue;
            }

            if (key.ChangeType == DiffChangeType.Deleted && key.ValueDiffs.Count == 0)
            {
                sb.AppendLine($"{Escape(key.KeyPath)},,Deleted,,{key.IsPersistenceRisk},{Escape(key.RiskCategory)},,");
                continue;
            }

            foreach (var val in key.ValueDiffs)
            {
                sb.AppendLine($"{Escape(key.KeyPath)},{Escape(val.ValueName)},{val.ChangeType},{Escape(val.ValueTypeDisplay)},{key.IsPersistenceRisk},{Escape(key.RiskCategory)},{Escape(val.BeforeDisplay)},{Escape(val.AfterDisplay)}");
            }
        }

        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return $"\"{value}\"";
    }
}
