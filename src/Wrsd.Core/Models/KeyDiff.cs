namespace Wrsd.Core.Models;

public sealed class KeyDiff
{
    public required string KeyPath { get; init; }
    public required DiffChangeType ChangeType { get; init; }
    public List<ValueDiff> ValueDiffs { get; init; } = new();

    public RegistryKeySnapshot? Before { get; init; }
    public RegistryKeySnapshot? After { get; init; }

    public bool HasValueChanges => ValueDiffs.Count > 0;

    public bool IsNoise { get; set; }
    public bool IsPersistenceRisk { get; set; }
    public string? RiskCategory { get; set; }

    public int AddedValuesCount => ValueDiffs.Count(v => v.ChangeType == DiffChangeType.Added);
    public int DeletedValuesCount => ValueDiffs.Count(v => v.ChangeType == DiffChangeType.Deleted);
    public int ModifiedValuesCount => ValueDiffs.Count(v => v.ChangeType == DiffChangeType.Modified);
}
