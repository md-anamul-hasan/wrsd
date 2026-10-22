using System.Diagnostics;
using Wrsd.Core.Diff;
using Wrsd.Core.Exporters;
using Wrsd.Core.Models;
using Wrsd.Core.Scanning;
using Wrsd.Core.Storage;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("==================================================");
Console.WriteLine("  WRSD - Windows Registry Snapshot Diff CLI");
Console.WriteLine("==================================================");
Console.ResetColor();

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintUsage();
    return 0;
}

string command = args[0].ToLowerInvariant();

try
{
    switch (command)
    {
        case "snapshot":
            return await HandleSnapshotAsync(args[1..]);

        case "diff":
            return await HandleDiffAsync(args[1..]);

        default:
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Unknown command: {command}");
            Console.ResetColor();
            PrintUsage();
            return 1;
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: {ex.Message}");
    Console.ResetColor();
    return 2;
}

static void PrintUsage()
{
    Console.WriteLine("""
Usage:
  wrsd snapshot <output.wrsd> [--preset quick|software|services|persistence|full]
  wrsd diff <baseline.wrsd> <target.wrsd> [options]

Diff Options:
  --undo-reg <file.reg>       Export rollback .REG script
  --redo-reg <file.reg>       Export apply .REG script
  --json <file.json>          Export JSON report
  --html <file.html>          Export HTML report
  --include-noise             Include background OS telemetry noise
  --persistence-only          Show only persistence and autorun hooks
""");
}

static async Task<int> HandleSnapshotAsync(string[] args)
{
    if (args.Length == 0)
    {
        Console.WriteLine("Missing output path. Example: wrsd snapshot baseline.wrsd");
        return 1;
    }

    string outputPath = args[0];
    var preset = SnapshotScopePreset.QuickTriage;

    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--preset" && i + 1 < args.Length)
        {
            preset = args[i + 1].ToLowerInvariant() switch
            {
                "full" => SnapshotScopePreset.FullSystem,
                "software" => SnapshotScopePreset.SoftwareOnly,
                "services" => SnapshotScopePreset.SystemServices,
                "persistence" => SnapshotScopePreset.PersistenceOnly,
                _ => SnapshotScopePreset.QuickTriage
            };
        }
    }

    Console.WriteLine($"Capturing snapshot using preset: {preset}...");
    var scanner = new RegistryScanner();
    var progress = new Progress<RegistryScanProgress>(p =>
    {
        Console.Write($"\r[Scanning] Keys: {p.KeysScanned:N0} | Values: {p.ValuesScanned:N0} | Elapsed: {p.Elapsed.TotalSeconds:F1}s    ");
    });

    var snapshot = await scanner.TakeSnapshotAsync(preset, progress: progress);
    Console.WriteLine();
    Console.WriteLine($"Snapshot complete in {snapshot.Duration.TotalSeconds:F2}s. Total keys: {snapshot.KeyCount:N0}, values: {snapshot.ValueCount:N0}.");

    Console.WriteLine($"Saving compressed snapshot to: {outputPath}...");
    await SnapshotSerializer.SaveToFileAsync(snapshot, outputPath);
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Snapshot saved successfully.");
    Console.ResetColor();

    return 0;
}

static async Task<int> HandleDiffAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.WriteLine("Usage: wrsd diff <baseline.wrsd> <target.wrsd> [options]");
        return 1;
    }

    string basePath = args[0];
    string targetPath = args[1];

    if (!File.Exists(basePath))
    {
        Console.WriteLine($"Baseline snapshot file not found: {basePath}");
        return 1;
    }

    if (!File.Exists(targetPath))
    {
        Console.WriteLine($"Target snapshot file not found: {targetPath}");
        return 1;
    }

    Console.WriteLine($"Loading baseline: {basePath}...");
    var baseline = await SnapshotSerializer.LoadFromFileAsync(basePath);
    Console.WriteLine($"Loading target: {targetPath}...");
    var target = await SnapshotSerializer.LoadFromFileAsync(targetPath);

    Console.WriteLine("Calculating diff...");
    var report = SnapshotDiffEngine.Compare(baseline, target);

    bool hideNoise = true;
    bool persistenceOnly = false;
    string? undoRegPath = null;
    string? redoRegPath = null;
    string? jsonPath = null;
    string? htmlPath = null;

    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "--include-noise") hideNoise = false;
        else if (args[i] == "--persistence-only") persistenceOnly = true;
        else if (args[i] == "--undo-reg" && i + 1 < args.Length) undoRegPath = args[++i];
        else if (args[i] == "--redo-reg" && i + 1 < args.Length) redoRegPath = args[++i];
        else if (args[i] == "--json" && i + 1 < args.Length) jsonPath = args[++i];
        else if (args[i] == "--html" && i + 1 < args.Length) htmlPath = args[++i];
    }

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"--- Diff Results (Duration: {report.CalculationTime.TotalMilliseconds:F1}ms) ---");
    Console.ResetColor();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  [+] Added Keys:     {report.AddedKeysCount:N0} (Values: {report.AddedValuesCount:N0})");
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  [-] Deleted Keys:   {report.DeletedKeysCount:N0} (Values: {report.DeletedValuesCount:N0})");
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  [~] Modified Keys:  {report.ModifiedKeysCount:N0} (Values: {report.ModifiedValuesCount:N0})");
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine($"  [!] Persistence:    {report.PersistenceRisksCount:N0} flagged hooks");
    Console.ResetColor();
    Console.WriteLine($"  [*] Telemetry Noise:{report.NoiseKeysCount:N0} keys");

    if (persistenceOnly && report.PersistenceRisksCount > 0)
    {
        Console.WriteLine("\nFlagged Persistence Entries:");
        foreach (var r in report.Filter(hideNoise: hideNoise, persistenceOnly: true).Take(10))
        {
            Console.WriteLine($"  -> [{r.RiskCategory}] {r.KeyPath}");
        }
    }

    if (undoRegPath != null)
    {
        Console.WriteLine($"Exporting Undo script to {undoRegPath}...");
        string undoContent = RegFileExporter.GenerateUndoReg(report, includeNoise: !hideNoise);
        await File.WriteAllTextAsync(undoRegPath, undoContent);
        Console.WriteLine("Undo script exported.");
    }

    if (redoRegPath != null)
    {
        Console.WriteLine($"Exporting Redo script to {redoRegPath}...");
        string redoContent = RegFileExporter.GenerateRedoReg(report, includeNoise: !hideNoise);
        await File.WriteAllTextAsync(redoRegPath, redoContent);
        Console.WriteLine("Redo script exported.");
    }

    if (jsonPath != null)
    {
        Console.WriteLine($"Exporting JSON report to {jsonPath}...");
        string jsonContent = JsonExporter.ExportReport(report, hideNoise: hideNoise);
        await File.WriteAllTextAsync(jsonPath, jsonContent);
        Console.WriteLine("JSON report exported.");
    }

    if (htmlPath != null)
    {
        Console.WriteLine($"Exporting HTML report to {htmlPath}...");
        string htmlContent = HtmlReportExporter.GenerateHtml(report, hideNoise: hideNoise);
        await File.WriteAllTextAsync(htmlPath, htmlContent);
        Console.WriteLine("HTML report exported.");
    }

    return 0;
}
