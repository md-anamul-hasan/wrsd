using System.Text.Json;
using System.Text.Json.Serialization;
using Wrsd.Core.Models;

namespace Wrsd.Core.Exporters;

public static class JsonExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string ExportReport(SnapshotDiffReport report, bool hideNoise = true)
    {
        var filteredKeys = report.Filter(hideNoise: hideNoise).Select(k => new
        {
            k.KeyPath,
            ChangeType = k.ChangeType.ToString(),
            k.IsPersistenceRisk,
            k.RiskCategory,
            Values = k.ValueDiffs.Select(v => new
            {
                v.ValueName,
                ChangeType = v.ChangeType.ToString(),
                v.ValueTypeDisplay,
                Before = v.BeforeDisplay,
                After = v.AfterDisplay
            })
        });

        var root = new
        {
            GeneratedAt = report.GeneratedAt,
            CalculationTimeMs = report.CalculationTime.TotalMilliseconds,
            Baseline = new
            {
                report.Baseline.Id,
                report.Baseline.Name,
                report.Baseline.CreatedAt,
                report.Baseline.ScopeDescription,
                report.Baseline.KeyCount,
                report.Baseline.ValueCount
            },
            Target = new
            {
                report.Target.Id,
                report.Target.Name,
                report.Target.CreatedAt,
                report.Target.ScopeDescription,
                report.Target.KeyCount,
                report.Target.ValueCount
            },
            Statistics = new
            {
                report.AddedKeysCount,
                report.DeletedKeysCount,
                report.ModifiedKeysCount,
                report.TotalKeysChanged,
                report.AddedValuesCount,
                report.DeletedValuesCount,
                report.ModifiedValuesCount,
                report.TotalValuesChanged,
                report.PersistenceRisksCount,
                report.NoiseKeysCount
            },
            Changes = filteredKeys
        };

        return JsonSerializer.Serialize(root, Options);
    }
}
