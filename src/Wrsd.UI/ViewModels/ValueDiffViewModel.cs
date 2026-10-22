using Wrsd.Core.Models;

namespace Wrsd.UI.ViewModels;

public sealed class ValueDiffViewModel
{
    public ValueDiff Source { get; }

    public string ValueName => string.IsNullOrEmpty(Source.ValueName) ? "(Default)" : Source.ValueName;
    public string TypeDisplay => Source.ValueTypeDisplay;
    public DiffChangeType ChangeType => Source.ChangeType;
    public string BeforeDisplay => Source.BeforeDisplay;
    public string AfterDisplay => Source.AfterDisplay;

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

    public ValueDiffViewModel(ValueDiff source)
    {
        Source = source;
    }
}
