using System.Text;

namespace Wrsd.Core.Models;

public sealed class RegistryValueSnapshot : IEquatable<RegistryValueSnapshot>
{
    public string Name { get; init; } = string.Empty;
    public RegistryValueKind Kind { get; init; } = RegistryValueKind.None;
    public byte[] RawData { get; init; } = Array.Empty<byte>();

    // Cached string representation for fast display
    private string? _displayValue;
    public string DisplayValue => _displayValue ??= FormatDisplayValue();

    public static RegistryValueSnapshot Create(string name, RegistryValueKind kind, object? rawValue)
    {
        byte[] bytes = SerializeValue(kind, rawValue);
        return new RegistryValueSnapshot
        {
            Name = name,
            Kind = kind,
            RawData = bytes
        };
    }

    private static byte[] SerializeValue(RegistryValueKind kind, object? val)
    {
        if (val is null) return Array.Empty<byte>();

        try
        {
            return kind switch
            {
                RegistryValueKind.String or RegistryValueKind.ExpandString =>
                    Encoding.Unicode.GetBytes(val.ToString() ?? string.Empty),

                RegistryValueKind.DWord =>
                    BitConverter.GetBytes(Convert.ToInt32(val)),

                RegistryValueKind.QWord =>
                    BitConverter.GetBytes(Convert.ToInt64(val)),

                RegistryValueKind.Binary when val is byte[] b =>
                    b,

                RegistryValueKind.MultiString when val is string[] arr =>
                    Encoding.Unicode.GetBytes(string.Join("\0", arr) + "\0\0"),

                _ => val is byte[] raw ? raw : Encoding.Unicode.GetBytes(val.ToString() ?? string.Empty)
            };
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    private string FormatDisplayValue()
    {
        if (RawData.Length == 0)
        {
            return Kind switch
            {
                RegistryValueKind.DWord => "0x00000000 (0)",
                RegistryValueKind.QWord => "0x0000000000000000 (0)",
                RegistryValueKind.Binary => "(zero-length binary value)",
                _ => string.Empty
            };
        }

        try
        {
            switch (Kind)
            {
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString:
                    return Encoding.Unicode.GetString(RawData).TrimEnd('\0');

                case RegistryValueKind.DWord:
                    if (RawData.Length >= 4)
                    {
                        uint dword = BitConverter.ToUInt32(RawData, 0);
                        return $"0x{dword:X8} ({dword})";
                    }
                    break;

                case RegistryValueKind.QWord:
                    if (RawData.Length >= 8)
                    {
                        ulong qword = BitConverter.ToUInt64(RawData, 0);
                        return $"0x{qword:X16} ({qword})";
                    }
                    break;

                case RegistryValueKind.MultiString:
                    string decoded = Encoding.Unicode.GetString(RawData);
                    string[] parts = decoded.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
                    return string.Join(" | ", parts);

                case RegistryValueKind.Binary:
                    if (RawData.Length <= 32)
                    {
                        return BitConverter.ToString(RawData).Replace("-", " ");
                    }
                    return $"{BitConverter.ToString(RawData, 0, 32).Replace("-", " ")}... ({RawData.Length} bytes)";

                default:
                    return $"Binary ({RawData.Length} bytes)";
            }
        }
        catch
        {
            // fallback
        }

        return BitConverter.ToString(RawData).Replace("-", " ");
    }

    public bool Equals(RegistryValueSnapshot? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (!string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)) return false;
        if (Kind != other.Kind) return false;
        return RawData.AsSpan().SequenceEqual(other.RawData.AsSpan());
    }

    public override bool Equals(object? obj) => Equals(obj as RegistryValueSnapshot);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Name, StringComparer.OrdinalIgnoreCase);
        hash.Add(Kind);
        hash.Add(RawData.Length);
        if (RawData.Length > 0)
        {
            hash.Add(RawData[0]);
            hash.Add(RawData[^1]);
        }
        return hash.ToHashCode();
    }
}
