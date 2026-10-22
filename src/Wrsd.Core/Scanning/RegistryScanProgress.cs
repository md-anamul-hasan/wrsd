namespace Wrsd.Core.Scanning;

public sealed class RegistryScanProgress
{
    public int KeysScanned { get; init; }
    public int ValuesScanned { get; init; }
    public string CurrentPath { get; init; } = string.Empty;
    public TimeSpan Elapsed { get; init; }
}
