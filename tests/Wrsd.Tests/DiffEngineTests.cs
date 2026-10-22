using Wrsd.Core.Diff;
using Wrsd.Core.Models;
using Xunit;

namespace Wrsd.Tests;

public class DiffEngineTests
{
    [Fact]
    public void Compare_DetectsAddedAndDeletedKeys()
    {
        var baseline = new RegistrySnapshot
        {
            Name = "Baseline",
            Keys = new Dictionary<string, RegistryKeySnapshot>
            {
                ["HKLM\\SOFTWARE\\OldApp"] = new RegistryKeySnapshot
                {
                    Path = "HKLM\\SOFTWARE\\OldApp",
                    Values = new Dictionary<string, RegistryValueSnapshot>
                    {
                        ["Version"] = RegistryValueSnapshot.Create("Version", RegistryValueKind.String, "1.0")
                    }
                }
            }
        };

        var target = new RegistrySnapshot
        {
            Name = "Target",
            Keys = new Dictionary<string, RegistryKeySnapshot>
            {
                ["HKLM\\SOFTWARE\\NewApp"] = new RegistryKeySnapshot
                {
                    Path = "HKLM\\SOFTWARE\\NewApp",
                    Values = new Dictionary<string, RegistryValueSnapshot>
                    {
                        ["InstallPath"] = RegistryValueSnapshot.Create("InstallPath", RegistryValueKind.String, "C:\\NewApp")
                    }
                }
            }
        };

        var report = SnapshotDiffEngine.Compare(baseline, target);

        Assert.Equal(1, report.AddedKeysCount);
        Assert.Equal(1, report.DeletedKeysCount);
        Assert.Equal(0, report.ModifiedKeysCount);

        var addedKey = report.AllKeyDiffs.First(k => k.ChangeType == DiffChangeType.Added);
        Assert.Equal("HKLM\\SOFTWARE\\NewApp", addedKey.KeyPath);
        Assert.Single(addedKey.ValueDiffs);
        Assert.Equal(DiffChangeType.Added, addedKey.ValueDiffs[0].ChangeType);

        var deletedKey = report.AllKeyDiffs.First(k => k.ChangeType == DiffChangeType.Deleted);
        Assert.Equal("HKLM\\SOFTWARE\\OldApp", deletedKey.KeyPath);
        Assert.Single(deletedKey.ValueDiffs);
        Assert.Equal(DiffChangeType.Deleted, deletedKey.ValueDiffs[0].ChangeType);
    }

    [Fact]
    public void Compare_DetectsModifiedValues()
    {
        var baseline = new RegistrySnapshot
        {
            Name = "Baseline",
            Keys = new Dictionary<string, RegistryKeySnapshot>
            {
                ["HKCU\\Software\\MyTool"] = new RegistryKeySnapshot
                {
                    Path = "HKCU\\Software\\MyTool",
                    Values = new Dictionary<string, RegistryValueSnapshot>
                    {
                        ["Counter"] = RegistryValueSnapshot.Create("Counter", RegistryValueKind.DWord, 10),
                        ["UnchangedVal"] = RegistryValueSnapshot.Create("UnchangedVal", RegistryValueKind.String, "Same")
                    }
                }
            }
        };

        var target = new RegistrySnapshot
        {
            Name = "Target",
            Keys = new Dictionary<string, RegistryKeySnapshot>
            {
                ["HKCU\\Software\\MyTool"] = new RegistryKeySnapshot
                {
                    Path = "HKCU\\Software\\MyTool",
                    Values = new Dictionary<string, RegistryValueSnapshot>
                    {
                        ["Counter"] = RegistryValueSnapshot.Create("Counter", RegistryValueKind.DWord, 20),
                        ["UnchangedVal"] = RegistryValueSnapshot.Create("UnchangedVal", RegistryValueKind.String, "Same"),
                        ["NewFlag"] = RegistryValueSnapshot.Create("NewFlag", RegistryValueKind.DWord, 1)
                    }
                }
            }
        };

        var report = SnapshotDiffEngine.Compare(baseline, target);

        Assert.Equal(0, report.AddedKeysCount);
        Assert.Equal(0, report.DeletedKeysCount);
        Assert.Equal(1, report.ModifiedKeysCount);

        var key = report.AllKeyDiffs[0];
        Assert.Equal(2, key.ValueDiffs.Count);

        var modifiedVal = key.ValueDiffs.First(v => v.ValueName == "Counter");
        Assert.Equal(DiffChangeType.Modified, modifiedVal.ChangeType);
        Assert.True(modifiedVal.IsDataModified);

        var addedVal = key.ValueDiffs.First(v => v.ValueName == "NewFlag");
        Assert.Equal(DiffChangeType.Added, addedVal.ChangeType);
    }
}
