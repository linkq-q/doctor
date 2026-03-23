using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace URPSceneDoctor.Editor
{
    public static class USD_DeltaExtractor
    {
        public static USD_DeltaPatch Extract(string sceneName, string beforePath, string afterPath)
        {
            var before = USD_SnapshotUtil.LoadSnapshot(beforePath);
            var after = USD_SnapshotUtil.LoadSnapshot(afterPath);
            if (before == null || after == null) return null;

            var patch = new USD_DeltaPatch
            {
                sceneName = sceneName,
                beforeSnapshotPath = beforePath,
                afterSnapshotPath = afterPath
            };

            AddIfChanged(patch, "URP.shadowDistance", before.shadowDistance, after.shadowDistance, "(PC typical)", true);
            AddIfChanged(patch, "URP.renderScale", before.renderScale, after.renderScale, string.Empty, true);
            AddIfChanged(patch, "URP.msaaSampleCount", before.msaaSampleCount, after.msaaSampleCount, string.Empty, false);
            AddIfChanged(patch, "Volume.hasGlobalVolume", before.hasGlobalVolume, after.hasGlobalVolume, string.Empty, false);
            AddIfChanged(patch, "Volume.enabledOverridesCount", before.enabledOverrides.Count, after.enabledOverrides.Count, string.Empty, false);

            AddVolumeKeyDiffs(patch, before, after);
            AddIfChanged(patch, "Curves.preset", before.curvePreset ?? string.Empty, after.curvePreset ?? string.Empty);
            AddIfChanged(patch, "SMH.shadowsBias", before.smhShadowsBias, after.smhShadowsBias, string.Empty, true);
            AddIfChanged(patch, "SMH.highlightsBias", before.smhHighlightsBias, after.smhHighlightsBias, string.Empty, true);


            if (patch.changedFields.Count > 0)
            {
                foreach (var field in patch.changedFields)
                {
                    patch.recommendedRanges.Add($"{field.path} often {field.deltaHint}");
                }
            }

            return patch;
        }

        private static void AddVolumeKeyDiffs(USD_DeltaPatch patch, USD_ScanSnapshot before, USD_ScanSnapshot after)
        {
            var keys = new HashSet<string>(before.volumeKeyValues.Keys);
            keys.UnionWith(after.volumeKeyValues.Keys);
            foreach (var key in keys.OrderBy(x => x))
            {
                var b = before.volumeKeyValues.ContainsKey(key) ? before.volumeKeyValues[key] : 0f;
                var a = after.volumeKeyValues.ContainsKey(key) ? after.volumeKeyValues[key] : 0f;
                var d = a - b;
                if (System.Math.Abs(d) <= 0.001f) continue;

                patch.changedFields.Add(new USD_DeltaField
                {
                    path = "VolumeKey." + key,
                    before = b.ToString("0.000", CultureInfo.InvariantCulture),
                    after = a.ToString("0.000", CultureInfo.InvariantCulture),
                    deltaHint = d.ToString("+0.000;-0.000", CultureInfo.InvariantCulture)
                });

                patch.recommendedRanges.Add($"VolumeKey.{key} changed by {d.ToString("+0.###;-0.###", CultureInfo.InvariantCulture)}");
            }
        }


        private static void AddIfChanged(USD_DeltaPatch patch, string path, string before, string after)
        {
            if (before == after) return;
            patch.changedFields.Add(new USD_DeltaField { path = path, before = before, after = after, deltaHint = $"{before} -> {after}" });
        }

        private static void AddIfChanged(USD_DeltaPatch patch, string path, float before, float after, string suffix, bool numericDelta)
        {
            if (before == after) return;
            var delta = (after - before).ToString("+0.##;-0.##", CultureInfo.InvariantCulture);
            patch.changedFields.Add(new USD_DeltaField
            {
                path = path,
                before = before.ToString(CultureInfo.InvariantCulture),
                after = after.ToString(CultureInfo.InvariantCulture),
                deltaHint = numericDelta ? delta + " " + suffix : delta
            });
        }

        private static void AddIfChanged(USD_DeltaPatch patch, string path, int before, int after, string suffix, bool numericDelta)
        {
            if (before == after) return;
            var delta = (after - before).ToString("+0;-0");
            patch.changedFields.Add(new USD_DeltaField { path = path, before = before.ToString(), after = after.ToString(), deltaHint = numericDelta ? delta + " " + suffix : delta });
        }

        private static void AddIfChanged(USD_DeltaPatch patch, string path, bool before, bool after, string suffix, bool numericDelta)
        {
            if (before == after) return;
            patch.changedFields.Add(new USD_DeltaField { path = path, before = before.ToString(), after = after.ToString(), deltaHint = $"{before} -> {after}" });
        }
    }
}
