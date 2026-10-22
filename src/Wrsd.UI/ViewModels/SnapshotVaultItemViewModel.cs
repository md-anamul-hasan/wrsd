namespace Wrsd.UI.ViewModels;

public sealed class SnapshotVaultItemViewModel : ObservableObject
{
    private bool _isSelected;

    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public DateTime CreatedAt { get; init; }
    public int KeyCount { get; init; }
    public int ValueCount { get; init; }
    public string ScopePreset { get; init; } = "Quick Triage";
    public string FileSizeBytes { get; init; } = "0 KB";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
