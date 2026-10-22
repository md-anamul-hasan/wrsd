namespace Wrsd.Core.Models;

public sealed class RegistryKeySnapshot
{
    public string Path { get; init; } = string.Empty;
    public Dictionary<string, RegistryValueSnapshot> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> SubKeyNames { get; init; } = new();

    public string KeyName => System.IO.Path.GetFileName(Path) is { Length: > 0 } name ? name : Path;

    public RegistryValueSnapshot? GetValue(string name)
    {
        Values.TryGetValue(name, out var val);
        return val;
    }
}
