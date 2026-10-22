using System.Reflection;

namespace Wrsd.UI;

/// <summary>Product label for the status bar ("WRSD v1.0.0"), read from the assembly version so it follows the csproj.</summary>
public static class AppInfo
{
    public static string Label { get; } = "WRSD v" + (Assembly.GetEntryAssembly()?.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "");
}
