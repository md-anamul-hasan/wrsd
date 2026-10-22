namespace Wrsd.Core.Models;

public sealed class ValueDiff
{
    public required string KeyPath { get; init; }
    public required string ValueName { get; init; }
    public required DiffChangeType ChangeType { get; init; }
    public RegistryValueSnapshot? Before { get; init; }
    public RegistryValueSnapshot? After { get; init; }

    public bool IsTypeModified =>
        Before is not null && After is not null && Before.Kind != After.Kind;

    public bool IsDataModified =>
        Before is not null && After is not null && !Before.RawData.AsSpan().SequenceEqual(After.RawData.AsSpan());

    public string ValueTypeDisplay =>
        After?.Kind.ToString() ?? Before?.Kind.ToString() ?? "Unknown";

    public string BeforeDisplay => Before?.DisplayValue ?? "(none)";
    public string AfterDisplay => After?.DisplayValue ?? "(none)";
}
