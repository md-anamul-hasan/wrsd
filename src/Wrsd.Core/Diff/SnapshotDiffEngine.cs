using System.Diagnostics;
using Wrsd.Core.Models;
using Wrsd.Core.Noise;
using Wrsd.Core.Triage;

namespace Wrsd.Core.Diff;

public static class SnapshotDiffEngine
{
    public static SnapshotDiffReport Compare(RegistrySnapshot baseline, RegistrySnapshot target)
    {
        var sw = Stopwatch.StartNew();
        var keyDiffs = new List<KeyDiff>();

        var allKeyPaths = new HashSet<string>(baseline.Keys.Keys, StringComparer.OrdinalIgnoreCase);
        allKeyPaths.UnionWith(target.Keys.Keys);

        foreach (var path in allKeyPaths)
        {
            bool inBaseline = baseline.Keys.TryGetValue(path, out var baseKey);
            bool inTarget = target.Keys.TryGetValue(path, out var targetKey);

            if (inBaseline && !inTarget)
            {
                // Key was deleted
                var valueDiffs = new List<ValueDiff>();
                if (baseKey != null)
                {
                    foreach (var v in baseKey.Values.Values)
                    {
                        valueDiffs.Add(new ValueDiff
                        {
                            KeyPath = path,
                            ValueName = v.Name,
                            ChangeType = DiffChangeType.Deleted,
                            Before = v,
                            After = null
                        });
                    }
                }

                var (isRisk, category) = PersistenceClassifier.CheckPersistenceRisk(path);
                keyDiffs.Add(new KeyDiff
                {
                    KeyPath = path,
                    ChangeType = DiffChangeType.Deleted,
                    Before = baseKey,
                    After = null,
                    ValueDiffs = valueDiffs,
                    IsNoise = NoiseClassifier.IsNoise(path),
                    IsPersistenceRisk = isRisk,
                    RiskCategory = category
                });
            }
            else if (!inBaseline && inTarget)
            {
                // Key was added
                var valueDiffs = new List<ValueDiff>();
                if (targetKey != null)
                {
                    foreach (var v in targetKey.Values.Values)
                    {
                        valueDiffs.Add(new ValueDiff
                        {
                            KeyPath = path,
                            ValueName = v.Name,
                            ChangeType = DiffChangeType.Added,
                            Before = null,
                            After = v
                        });
                    }
                }

                var (isRisk, category) = PersistenceClassifier.CheckPersistenceRisk(path);
                keyDiffs.Add(new KeyDiff
                {
                    KeyPath = path,
                    ChangeType = DiffChangeType.Added,
                    Before = null,
                    After = targetKey,
                    ValueDiffs = valueDiffs,
                    IsNoise = NoiseClassifier.IsNoise(path),
                    IsPersistenceRisk = isRisk,
                    RiskCategory = category
                });
            }
            else if (inBaseline && inTarget && baseKey != null && targetKey != null)
            {
                // Key exists in both - compare values and structure
                var valueDiffs = CompareValues(path, baseKey, targetKey);

                // Subkeys comparison
                bool subkeysChanged = !baseKey.SubKeyNames.OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(targetKey.SubKeyNames.OrderBy(s => s, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

                if (valueDiffs.Count > 0 || subkeysChanged)
                {
                    var (isRisk, category) = PersistenceClassifier.CheckPersistenceRisk(path);
                    keyDiffs.Add(new KeyDiff
                    {
                        KeyPath = path,
                        ChangeType = DiffChangeType.Modified,
                        Before = baseKey,
                        After = targetKey,
                        ValueDiffs = valueDiffs,
                        IsNoise = NoiseClassifier.IsNoise(path),
                        IsPersistenceRisk = isRisk,
                        RiskCategory = category
                    });
                }
            }
        }

        sw.Stop();

        return new SnapshotDiffReport
        {
            Baseline = baseline,
            Target = target,
            CalculationTime = sw.Elapsed,
            AllKeyDiffs = keyDiffs.OrderBy(k => k.KeyPath, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static List<ValueDiff> CompareValues(string path, RegistryKeySnapshot baseKey, RegistryKeySnapshot targetKey)
    {
        var diffs = new List<ValueDiff>();

        var allValueNames = new HashSet<string>(baseKey.Values.Keys, StringComparer.OrdinalIgnoreCase);
        allValueNames.UnionWith(targetKey.Values.Keys);

        foreach (var valName in allValueNames)
        {
            bool inBase = baseKey.Values.TryGetValue(valName, out var bVal);
            bool inTarget = targetKey.Values.TryGetValue(valName, out var tVal);

            if (inBase && !inTarget)
            {
                diffs.Add(new ValueDiff
                {
                    KeyPath = path,
                    ValueName = valName,
                    ChangeType = DiffChangeType.Deleted,
                    Before = bVal,
                    After = null
                });
            }
            else if (!inBase && inTarget)
            {
                diffs.Add(new ValueDiff
                {
                    KeyPath = path,
                    ValueName = valName,
                    ChangeType = DiffChangeType.Added,
                    Before = null,
                    After = tVal
                });
            }
            else if (inBase && inTarget && bVal != null && tVal != null)
            {
                if (!bVal.Equals(tVal))
                {
                    diffs.Add(new ValueDiff
                    {
                        KeyPath = path,
                        ValueName = valName,
                        ChangeType = DiffChangeType.Modified,
                        Before = bVal,
                        After = tVal
                    });
                }
            }
        }

        return diffs;
    }
}
