namespace Wrsd.Core.Models;

public sealed class RegistrySnapshot
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Snapshot";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string ScopeDescription { get; init; } = string.Empty;
    public SnapshotScopePreset ScopePreset { get; init; } = SnapshotScopePreset.QuickTriage;
    public TimeSpan Duration { get; set; }
    
    // Flat dictionary mapping KeyPath (uppercase or normalized) -> RegistryKeySnapshot
    public Dictionary<string, RegistryKeySnapshot> Keys { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public int KeyCount => Keys.Count;

    public int ValueCount
    {
        get
        {
            int sum = 0;
            foreach (var key in Keys.Values)
            {
                sum += key.Values.Count;
            }
            return sum;
        }
    }

    public long EstimatedMemoryBytes
    {
        get
        {
            long bytes = 0;
            foreach (var kvp in Keys)
            {
                bytes += (kvp.Key.Length * 2) + 64;
                foreach (var v in kvp.Value.Values.Values)
                {
                    bytes += (v.Name.Length * 2) + v.RawData.Length + 48;
                }
            }
            return bytes;
        }
    }
}
