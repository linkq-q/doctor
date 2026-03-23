using System;
using System.Collections.Generic;

namespace URPSceneDoctor.Editor
{
    public enum USD_BrightnessBucket { High, Mid, Low }

    [Serializable]
    public sealed class USD_PolicyCheckResult
    {
        public int passed;
        public int warnings;
        public List<string> items = new List<string>();
    }

    public static class USD_PolicyChecker
    {
        public static USD_PolicyCheckResult Evaluate(USD_ScanSnapshot snapshot, USD_TastePolicyAsset policy, USD_BrightnessBucket bucket)
        {
            var r = new USD_PolicyCheckResult();
            if (snapshot == null || policy == null)
            {
                r.items.Add("[WARN] Policy check skipped: missing snapshot or policy.");
                r.warnings++;
                return r;
            }

            CheckHardRule(snapshot, policy, "Vig.intensity", r);
            CheckHardRule(snapshot, policy, "Vig.smoothness", r);
            CheckHardRule(snapshot, policy, "Grain.intensity", r);
            CheckHardRule(snapshot, policy, "Bloom.scatter", r);

            if (!snapshot.TryGetVolumeKey("Bloom.intensity", out var bloomIntensity))
            {
                r.items.Add("[WARN] Bloom.intensity missing.");
                r.warnings++;
            }
            else
            {
                var target = bucket == USD_BrightnessBucket.High ? policy.bloomHighTarget : (bucket == USD_BrightnessBucket.Mid ? policy.bloomMidTarget : policy.bloomLowTarget);
                var allowed = bucket == USD_BrightnessBucket.High ? 0.05f : (bucket == USD_BrightnessBucket.Mid ? 0.3f : 2f);
                var clampedTarget = Math.Min(target, policy.bloomCeiling);
                if (Math.Abs(bloomIntensity - clampedTarget) <= allowed)
                {
                    r.items.Add($"[PASS] Bloom.intensity fits {bucket} bucket ({bloomIntensity:0.###} ~ {clampedTarget:0.###}).");
                    r.passed++;
                }
                else
                {
                    r.items.Add($"[WARN] Bloom.intensity={bloomIntensity:0.###} deviates from {bucket} target {clampedTarget:0.###} (±{allowed:0.###}).");
                    r.warnings++;
                }

                if (bloomIntensity > policy.bloomCeiling)
                {
                    r.items.Add($"[WARN] Bloom.intensity exceeds ceiling {policy.bloomCeiling:0.###}.");
                    r.warnings++;
                }
            }

            if (snapshot.TryGetVolumeKey("CA.contrast", out var contrast) && contrast > 20f)
            {
                r.items.Add("[WARN] High CA.contrast detected; prefer Curves/SMH first per methodology.");
                r.warnings++;
            }
            else
            {
                r.items.Add("[PASS] Contrast strategy looks acceptable for v1.0 heuristic.");
                r.passed++;
            }

            return r;
        }

        private static void CheckHardRule(USD_ScanSnapshot snapshot, USD_TastePolicyAsset policy, string key, USD_PolicyCheckResult r)
        {
            var hard = policy.hardRules.Find(x => x.key == key);
            if (hard == null) return;
            if (!snapshot.TryGetVolumeKey(key, out var value))
            {
                r.items.Add($"[WARN] {key} missing (required). target={hard.target:0.###}");
                r.warnings++;
                return;
            }

            if (Math.Abs(value - hard.target) <= hard.tolerance)
            {
                r.items.Add($"[PASS] {key}={value:0.###} within target {hard.target:0.###}±{hard.tolerance:0.###}");
                r.passed++;
            }
            else
            {
                r.items.Add($"[WARN] {key}={value:0.###} outside target {hard.target:0.###}±{hard.tolerance:0.###}");
                r.warnings++;
            }
        }
    }
}
