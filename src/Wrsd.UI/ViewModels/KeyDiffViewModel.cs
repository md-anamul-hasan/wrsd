using Wrsd.Core.Models;

namespace Wrsd.UI.ViewModels;

public sealed class KeyDiffViewModel
{
    private IReadOnlyList<ValueDiffViewModel>? _values;

    public KeyDiff Source { get; }
    public string KeyPath => Source.KeyPath;
    public DiffChangeType ChangeType => Source.ChangeType;
    public bool IsPersistenceRisk => Source.IsPersistenceRisk;
    public string? RiskCategory => Source.RiskCategory;
    public bool IsNoise => Source.IsNoise;

    public string BadgeText => Source.ChangeType switch
    {
        DiffChangeType.Added => "+ Added",
        DiffChangeType.Deleted => "- Removed",
        DiffChangeType.Modified => "~ Modified",
        _ => "Unchanged"
    };

    public string BadgeColor => Source.ChangeType switch
    {
        DiffChangeType.Added => "#10B981",     // Emerald
        DiffChangeType.Deleted => "#F43F5E",   // Coral Red
        DiffChangeType.Modified => "#F59E0B",  // Amber
        _ => "#8B949E"
    };

    public IReadOnlyList<ValueDiffViewModel> Values =>
        _values ??= Source.ValueDiffs.Select(v => new ValueDiffViewModel(v)).ToList();

    public KeyDiffViewModel(KeyDiff source)
    {
        Source = source;
    }
}
