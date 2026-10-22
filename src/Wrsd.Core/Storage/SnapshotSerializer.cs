using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wrsd.Core.Models;

namespace Wrsd.Core.Storage;

public static class SnapshotSerializer
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task SaveToFileAsync(RegistrySnapshot snapshot, string filePath, CancellationToken cancellationToken = default)
    {
        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var gzip = new GZipStream(fs, CompressionLevel.Optimal);
        await JsonSerializer.SerializeAsync(gzip, snapshot, JsonOpts, cancellationToken);
    }

    public static async Task<RegistrySnapshot> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var gzip = new GZipStream(fs, CompressionMode.Decompress);
        var snapshot = await JsonSerializer.DeserializeAsync<RegistrySnapshot>(gzip, JsonOpts, cancellationToken);
        return snapshot ?? throw new InvalidOperationException("Failed to deserialize registry snapshot.");
    }
}
