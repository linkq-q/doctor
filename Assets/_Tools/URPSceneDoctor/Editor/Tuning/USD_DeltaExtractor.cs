using System.Globalization;

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

            if (patch.changedFields.Count > 0)
            {
                foreach (var field in patch.changedFields)
                {
                    patch.recommendedRanges.Add($"{field.path} often {field.deltaHint}");
                }
            }

            return patch;
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
