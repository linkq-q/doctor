using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public static class USD_DeltaStatsUtil
    {
        public const string LibraryAssetPath = "Assets/_Tools/URPSceneDoctor/DeltaLibrary/USD_DeltaLibrary.asset";

        public static USD_DeltaLibraryAsset GetOrCreateLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<USD_DeltaLibraryAsset>(LibraryAssetPath);
            if (lib != null) return lib;
            USD_EditorUtil.EnsureFolder("Assets/_Tools/URPSceneDoctor/DeltaLibrary");
            lib = ScriptableObject.CreateInstance<USD_DeltaLibraryAsset>();
            AssetDatabase.CreateAsset(lib, LibraryAssetPath);
            AssetDatabase.SaveAssets();
            return lib;
        }

        public static USD_DeltaStats RecomputeStats(USD_DeltaLibraryAsset library)
        {
            var stats = new USD_DeltaStats { lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            if (library == null) return stats;
            stats.sampleCount = library.entries.Count;

            var fieldToDeltas = new Dictionary<string, List<float>>();
            var fieldCounts = new Dictionary<string, int>();

            foreach (var entry in library.entries)
            {
                stats.sampleRefs.Add($"{entry.sceneName}/{entry.timestamp}");
                var patch = USD_SnapshotUtil.LoadDeltaPatch(entry.deltaPatchPath);
                if (patch == null) continue;
                foreach (var changed in patch.changedFields)
                {
                    if (!fieldCounts.ContainsKey(changed.path)) fieldCounts[changed.path] = 0;
                    fieldCounts[changed.path]++;

                    if (TryParseFloat(changed.before, out var b) && TryParseFloat(changed.after, out var a))
                    {
                        if (!fieldToDeltas.ContainsKey(changed.path)) fieldToDeltas[changed.path] = new List<float>();
                        fieldToDeltas[changed.path].Add(a - b);
                    }
                }
            }

            foreach (var pair in fieldCounts.OrderByDescending(x => x.Value).Take(20))
            {
                var stat = new USD_LearningFieldStat { path = pair.Key, count = pair.Value };
                if (fieldToDeltas.TryGetValue(pair.Key, out var deltas) && deltas.Count > 0)
                {
                    var sorted = deltas.OrderBy(x => x).ToList();
                    stat.min = sorted.First();
                    stat.max = sorted.Last();
                    stat.mean = deltas.Average();
                    stat.median = sorted[sorted.Count / 2];
                    stat.positiveRate = deltas.Count(x => x > 0f) / (float)deltas.Count;
                    stat.recommendedRangeHint = $"usually {stat.min:+0.##;-0.##}~{stat.max:+0.##;-0.##}";
                }
                else
                {
                    stat.recommendedRangeHint = "usually changed";
                }

                stats.topChangedFields.Add(stat);
            }

            stats.topHints = BuildTopHints(stats);
            WriteStatsFiles(stats, library);
            return stats;
        }

        public static string GetLearnedHint(USD_DeltaStats stats, string fieldPath)
        {
            if (stats == null || stats.topChangedFields == null) return string.Empty;
            var found = stats.topChangedFields.FirstOrDefault(x => x.path == fieldPath);
            return found == null ? string.Empty : found.recommendedRangeHint;
        }

        private static List<string> BuildTopHints(USD_DeltaStats stats)
        {
            var hints = new List<string>();
            foreach (var item in stats.topChangedFields.Take(5))
            {
                hints.Add($"You often tune {item.path}: {item.recommendedRangeHint}.");
            }

            if (hints.Count == 0) hints.Add("No learning hints yet. Add tuning samples first.");
            return hints;
        }

        private static void WriteStatsFiles(USD_DeltaStats stats, USD_DeltaLibraryAsset library)
        {
            var folder = "Assets/_Tools/URPSceneDoctor/DeltaLibrary/Stats";
            USD_EditorUtil.EnsureFolder(folder);
            File.WriteAllText($"{folder}/delta_stats.json", JsonUtility.ToJson(stats, true));

            var sb = new StringBuilder();
            sb.AppendLine("# Delta Stats");
            sb.AppendLine($"- Sample count: {stats.sampleCount}");
            sb.AppendLine("## Samples");
            stats.sampleRefs.ForEach(x => sb.AppendLine("- " + x));
            sb.AppendLine("## Top changed fields");
            stats.topChangedFields.ForEach(x => sb.AppendLine($"- {x.path}: count={x.count}, {x.recommendedRangeHint}, positiveRate={x.positiveRate:0.00}"));
            sb.AppendLine("## Top 5 hints");
            stats.topHints.ForEach(x => sb.AppendLine("- " + x));
            File.WriteAllText($"{folder}/delta_stats.md", sb.ToString());

            library.lastUpdated = stats.lastUpdated;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static bool TryParseFloat(string s, out float value)
        {
            return float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }
    }
}
