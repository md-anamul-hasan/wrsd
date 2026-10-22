using System.IO;
using Xunit;
using Wrsd.UI;

namespace Wrsd.Tests;

public class CommandLineRuntimeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-root-" + Guid.NewGuid().ToString("N"));

    public CommandLineRuntimeTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { /* temp */ } }

    [Fact]
    public void EmptyRoot_IsReportedAsMissing() => Assert.False(CommandLineRuntime.IsInstalled(new[] { _root }));

    [Fact]
    public void OnlyOlderRuntime_IsReportedAsMissing()
    {
        Directory.CreateDirectory(Path.Combine(_root, "shared", "Microsoft.NETCore.App", "8.0.31"));
        Assert.False(CommandLineRuntime.IsInstalled(new[] { _root }));
    }

    [Fact]
    public void Runtime10_IsDetected()
    {
        Directory.CreateDirectory(Path.Combine(_root, "shared", "Microsoft.NETCore.App", "10.0.12"));
        Assert.True(CommandLineRuntime.IsInstalled(new[] { _root }));
    }

    [Fact]
    public void SecondRootIsSearched()
    {
        var other = Path.Combine(_root, "other");
        Directory.CreateDirectory(Path.Combine(other, "shared", "Microsoft.NETCore.App", "10.0.1"));
        Assert.True(CommandLineRuntime.IsInstalled(new[] { Path.Combine(_root, "missing"), other }));
    }

    [Fact]
    public void WingetId_IsTheOfficialRuntimePackage() => Assert.Equal("Microsoft.DotNet.Runtime.10", CommandLineRuntime.WingetId);
}
