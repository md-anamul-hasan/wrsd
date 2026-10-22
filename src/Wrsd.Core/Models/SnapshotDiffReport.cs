namespace Wrsd.Core.Models;

public sealed class SnapshotDiffReport
{
    public required RegistrySnapshot Baseline { get; init; }
    public required RegistrySnapshot Target { get; init; }
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan CalculationTime { get; init; }

    public List<KeyDiff> AllKeyDiffs { get; init; } = new();

    public int AddedKeysCount => AllKeyDiffs.Count(k => k.ChangeType == DiffChangeType.Added);
    public int DeletedKeysCount => AllKeyDiffs.Count(k => k.ChangeType == DiffChangeType.Deleted);
    public int ModifiedKeysCount => AllKeyDiffs.Count(k => k.ChangeType == DiffChangeType.Modified);
    public int TotalKeysChanged => AllKeyDiffs.Count;

    public int AddedValuesCount => AllKeyDiffs.Sum(k => k.AddedValuesCount);
    public int DeletedValuesCount => AllKeyDiffs.Sum(k => k.DeletedValuesCount);
    public int ModifiedValuesCount => AllKeyDiffs.Sum(k => k.ModifiedValuesCount);
    public int TotalValuesChanged => AddedValuesCount + DeletedValuesCount + ModifiedValuesCount;

    public int PersistenceRisksCount => AllKeyDiffs.Count(k => k.IsPersistenceRisk);
    public int NoiseKeysCount => AllKeyDiffs.Count(k => k.IsNoise);

    public IEnumerable<KeyDiff> Filter(
        bool hideNoise = true,
        bool persistenceOnly = false,
        string? searchText = null,
        DiffChangeType? changeTypeFilter = null)
    {
        var query = AllKeyDiffs.AsEnumerable();

        if (hideNoise)
        {
            query = query.Where(k => !k.IsNoise);
        }

        if (persistenceOnly)
        {
            query = query.Where(k => k.IsPersistenceRisk);
        }

        if (changeTypeFilter.HasValue)
        {
            query = query.Where(k => k.ChangeType == changeTypeFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string term = searchText.Trim();
            query = query.Where(k =>
                k.KeyPath.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                k.ValueDiffs.Any(v =>
                    v.ValueName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (v.After != null && v.After.DisplayValue.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (v.Before != null && v.Before.DisplayValue.Contains(term, StringComparison.OrdinalIgnoreCase))
                )
            );
        }

        return query;
    }
}
