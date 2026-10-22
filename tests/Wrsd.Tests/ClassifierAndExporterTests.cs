using Wrsd.Core.Diff;
using Wrsd.Core.Exporters;
using Wrsd.Core.Models;
using Wrsd.Core.Noise;
using Wrsd.Core.Storage;
using Wrsd.Core.Triage;
using Xunit;

namespace Wrsd.Tests;

public class ClassifierAndExporterTests
{
    [Theory]
    [InlineData(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist\{GUID}", true)]
    [InlineData(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\AppCompatCache", true)]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack", true)]
    [InlineData(@"HKLM\SOFTWARE\MyCompany\App", false)]
    [InlineData(@"HKCU\Software\CustomTool\Settings", false)]
    public void NoiseClassifier_IdentifiesNoiseCorrectly(string path, bool expectedNoise)
    {
        bool isNoise = NoiseClassifier.IsNoise(path);
        Assert.Equal(expectedNoise, isNoise);
    }

    [Theory]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true, "Standard Startup Autorun")]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", true, "Winlogon Shell / Userinit Hijack")]
    [InlineData(@"HKLM\SYSTEM\CurrentControlSet\Services\MaliciousService", true, "Service or Driver Installation")]
    [InlineData(@"HKLM\SOFTWARE\NormalSoftware\Config", false, null)]
    public void PersistenceClassifier_IdentifiesRisksCorrectly(string path, bool expectedRisk, string? expectedCategory)
    {
        var (isRisk, category) = PersistenceClassifier.CheckPersistenceRisk(path);
        Assert.Equal(expectedRisk, isRisk);
        if (expectedRisk)
        {
            Assert.Equal(expectedCategory, category);
        }
    }

    [Fact]
    public void RegFileExporter_GeneratesValidUndoScript()
    {
        var baseline = new RegistrySnapshot
        {
            Name = "Base",
            Keys = new Dictionary<string, RegistryKeySnapshot>
            {
                ["HKLM\\SOFTWARE\\Config"] = new RegistryKeySnapshot
                {
                    Path = "HKLM\\SOFTWARE\\Config",
                    Values = new Dictionary<string, RegistryValueSnapshot>
                    {
                        ["Timeout"] = RegistryValueSnapshot.Create("Timeout", RegistryValueKind.DWord, 30)
                    }
                }
            }
        };

        var target = new RegistrySnapshot
        {
            Name = "Target",
            Keys = new Dictionary<string, RegistryKeySnapshot>
            {
                ["HKLM\\SOFTWARE\\Config"] = new RegistryKeySnapshot
                {
                    Path = "HKLM\\SOFTWARE\\Config",
                    Values = new Dictionary<string, RegistryValueSnapshot>
                    {
                        ["Timeout"] = RegistryValueSnapshot.Create("Timeout", RegistryValueKind.DWord, 60),
                        ["NewInstallKey"] = RegistryValueSnapshot.Create("NewInstallKey", RegistryValueKind.String, "Installed")
                    }
                },
                ["HKLM\\SOFTWARE\\AddedApp"] = new RegistryKeySnapshot
                {
                    Path = "HKLM\\SOFTWARE\\AddedApp",
                    Values = new Dictionary<string, RegistryValueSnapshot>
                    {
                        ["Path"] = RegistryValueSnapshot.Create("Path", RegistryValueKind.String, "C:\\App")
                    }
                }
            }
        };

        var report = SnapshotDiffEngine.Compare(baseline, target);
        string undoReg = RegFileExporter.GenerateUndoReg(report);

        // Should contain command to delete the added key
        Assert.Contains("[-HKEY_LOCAL_MACHINE\\SOFTWARE\\AddedApp]", undoReg);
        // Should contain command to delete newly added value
        Assert.Contains("\"NewInstallKey\"=-", undoReg);
        // Should restore previous timeout
        Assert.Contains("\"Timeout\"=dword:0000001e", undoReg); // 30 in hex is 0x1e
    }

    [Fact]
    public async Task SnapshotSerializer_RoundtripPreservesData()
    {
        var original = new RegistrySnapshot
        {
            Name = "RoundtripTest",
            ScopeDescription = "TestScope",
            Keys = new Dictionary<string, RegistryKeySnapshot>
            {
                ["HKCU\\Software\\Test"] = new RegistryKeySnapshot
                {
                    Path = "HKCU\\Software\\Test",
                    Values = new Dictionary<string, RegistryValueSnapshot>
                    {
                        ["Title"] = RegistryValueSnapshot.Create("Title", RegistryValueKind.String, "Hello WRSD"),
                        ["Port"] = RegistryValueSnapshot.Create("Port", RegistryValueKind.DWord, 8080)
                    },
                    SubKeyNames = new List<string> { "SubKeyA", "SubKeyB" }
                }
            }
        };

        string tempFile = Path.Combine(Path.GetTempPath(), $"wrsd_test_{Guid.NewGuid():N}.wrsd");
        try
        {
            await SnapshotSerializer.SaveToFileAsync(original, tempFile);
            var loaded = await SnapshotSerializer.LoadFromFileAsync(tempFile);

            Assert.Equal(original.Name, loaded.Name);
            Assert.Equal(original.ScopeDescription, loaded.ScopeDescription);
            Assert.Single(loaded.Keys);

            var key = loaded.Keys["HKCU\\Software\\Test"];
            Assert.Equal(2, key.Values.Count);
            Assert.Equal("Hello WRSD", key.Values["Title"].DisplayValue);
            Assert.Equal(2, key.SubKeyNames.Count);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void RegFileExporter_GeneratesValidRedoScript()
    {
        var baseline = new RegistrySnapshot
        {
            Name = "Base",
            Keys = new Dictionary<string, RegistryKeySnapshot>
            {
                ["HKLM\\SOFTWARE\\Config"] = new RegistryKeySnapshot
                {
                    Path = "HKLM\\SOFTWARE\\Config",
                    Values = new Dictionary<string, RegistryValueSnapshot>
                    {
                        ["Timeout"] = RegistryValueSnapshot.Create("Timeout", RegistryValueKind.DWord, 30),
                        ["OldKey"] = RegistryValueSnapshot.Create("OldKey", RegistryValueKind.String, "Legacy")
                    }
                }
            }
        };

        var target = new RegistrySnapshot
        {
            Name = "Target",
            Keys = new Dictionary<string, RegistryKeySnapshot>
            {
                ["HKLM\\SOFTWARE\\Config"] = new RegistryKeySnapshot
                {
                    Path = "HKLM\\SOFTWARE\\Config",
                    Values = new Dictionary<string, RegistryValueSnapshot>
                    {
                        ["Timeout"] = RegistryValueSnapshot.Create("Timeout", RegistryValueKind.DWord, 60)
                    }
                },
                ["HKLM\\SOFTWARE\\AddedApp"] = new RegistryKeySnapshot
                {
                    Path = "HKLM\\SOFTWARE\\AddedApp",
                    Values = new Dictionary<string, RegistryValueSnapshot>
                    {
                        ["InstallDir"] = RegistryValueSnapshot.Create("InstallDir", RegistryValueKind.String, "C:\\App")
                    }
                }
            }
        };

        var report = SnapshotDiffEngine.Compare(baseline, target);
        string redoReg = RegFileExporter.GenerateRedoReg(report);

        // Should create AddedApp
        Assert.Contains("[HKEY_LOCAL_MACHINE\\SOFTWARE\\AddedApp]", redoReg);
        Assert.Contains("\"InstallDir\"=\"C:\\\\App\"", redoReg);

        // Should update Timeout
        Assert.Contains("\"Timeout\"=dword:0000003c", redoReg); // 60 in hex is 0x3c

        // Should delete OldKey
        Assert.Contains("\"OldKey\"=-", redoReg);
    }

    [Fact]
    public void CsvDiffExporter_ExportsValidCsv()
    {
        var baseline = new RegistrySnapshot
        {
            Name = "Base",
            Keys = new Dictionary<string, RegistryKeySnapshot>
            {
                ["HKLM\\SOFTWARE\\Config"] = new RegistryKeySnapshot
                {
                    Path = "HKLM\\SOFTWARE\\Config",
                    Values = new Dictionary<string, RegistryValueSnapshot>
                    {
                        ["Timeout"] = RegistryValueSnapshot.Create("Timeout", RegistryValueKind.DWord, 30)
                    }
                }
            }
        };

        var target = new RegistrySnapshot
        {
            Name = "Target",
            Keys = new Dictionary<string, RegistryKeySnapshot>
            {
                ["HKLM\\SOFTWARE\\Config"] = new RegistryKeySnapshot
                {
                    Path = "HKLM\\SOFTWARE\\Config",
                    Values = new Dictionary<string, RegistryValueSnapshot>
                    {
                        ["Timeout"] = RegistryValueSnapshot.Create("Timeout", RegistryValueKind.DWord, 60)
                    }
                },
                ["HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run"] = new RegistryKeySnapshot
                {
                    Path = "HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run",
                    Values = new Dictionary<string, RegistryValueSnapshot>
                    {
                        ["Malware"] = RegistryValueSnapshot.Create("Malware", RegistryValueKind.String, "C:\\evil.exe")
                    }
                }
            }
        };

        var diff = SnapshotDiffEngine.Compare(baseline, target);
        string csv = CsvDiffExporter.ExportToCsv(diff);

        Assert.Contains("KeyPath,ValueName,ChangeType,ValueType,IsPersistenceRisk,RiskCategory,BeforeData,AfterData", csv);
        Assert.Contains("HKLM\\SOFTWARE\\Config", csv);
        Assert.Contains("Timeout", csv);
        Assert.Contains("Malware", csv);
        Assert.Contains("Standard Startup Autorun", csv);
    }
}

