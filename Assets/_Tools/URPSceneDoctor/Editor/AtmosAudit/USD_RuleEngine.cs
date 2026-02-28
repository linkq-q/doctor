using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public static class USD_RuleEngine
    {
        private static readonly Dictionary<string, string> RuleFieldMap = new Dictionary<string, string>
        {
            { "R-P0-01", "Lighting.dirLightShadowsEnabled" },
            { "R-P0-02", "Volumes.hasGlobalVolume" },
            { "R-P0-03", "Lighting.ambientIntensity" },
            { "R-P0-04", "Lighting.hasAnyReflectionProbe" },
            { "R-P1-01", "Scene.materialCountDistinct" },
            { "R-P1-02", "Volumes.enabledOverrides" },
            { "R-P1-03", "Scene.shaderCountDistinct" },
            { "R-P1-04", "Volumes.enabledOverridesCount" },
            { "R-P1-05", "Scene.shaderCountDistinct" },
            { "R-P1-06", "Scene.transparentRendererCount" },
            { "R-P2-01", "Volumes.enabledOverrides" },
            { "R-P2-02", "Volumes.enabledOverrides" }
        };

        public static USD_RulePack LoadRulePack(string path)
        {
            if (!File.Exists(path)) return new USD_RulePack();
            return JsonUtility.FromJson<USD_RulePack>(File.ReadAllText(path));
        }

        public static List<USD_WorkOrder> Evaluate(USD_ScanSnapshot snapshot, USD_RulePack pack, USD_DeltaPatch deltaPatch = null, USD_TastePolicyAsset tastePolicy = null, USD_DeltaStats learningStats = null)
        {
            var orders = new List<USD_WorkOrder>();
            foreach (var rule in pack.rules)
            {
                if (!MatchAll(snapshot, rule.triggers)) continue;
                if (rule.excludes != null && rule.excludes.Count > 0 && MatchAll(snapshot, rule.excludes)) continue;

                var wo = new USD_WorkOrder
                {
                    id = rule.id,
                    title = rule.title,
                    severity = rule.severity,
                    category = rule.category,
                    subCategory = string.IsNullOrEmpty(rule.subCategory) ? "Readability" : rule.subCategory,
                    diagnosis = rule.diagnosisTemplate,
                    costNotes = string.IsNullOrEmpty(rule.notes) ? "Low to medium implementation cost." : rule.notes
                };
                wo.evidence.AddRange(BuildEvidence(snapshot, rule));
                wo.symptoms.AddRange(rule.symptoms);

                var learnedHint = string.Empty;
                if (learningStats != null && RuleFieldMap.TryGetValue(rule.id, out var fieldPath))
                {
                    learnedHint = USD_DeltaStatsUtil.GetLearnedHint(learningStats, fieldPath);
                }

                foreach (var p in rule.prescriptions)
                {
                    var range = p.recommendedRange;
                    if (deltaPatch != null && deltaPatch.recommendedRanges.Count > 0)
                    {
                        range += " (Personal delta hint) " + string.Join("; ", deltaPatch.recommendedRanges);
                    }

                    if (!string.IsNullOrEmpty(learnedHint))
                    {
                        range += " (Learned) " + learnedHint;
                    }

                    if (tastePolicy != null)
                    {
                        range += $" (TastePolicy) prefer: {string.Join(" > ", tastePolicy.priorityOrder)}; avoid: {string.Join(", ", tastePolicy.forbiddenActions)}";
                    }

                    wo.prescriptions.Add(new USD_Prescription
                    {
                        priority = p.priority,
                        actionText = p.actionText,
                        targetPathHint = p.targetPathHint,
                        recommendedRange = range,
                        alternatives = new List<string>(p.alternatives)
                    });
                }

                wo.verification = new USD_Verification
                {
                    steps = new List<string>(rule.verificationTemplate),
                    referenceShots = new List<string> { "MainCam_Wide", "MainCam_Mid", "MainCam_Close" },
                    performanceBudgetHint = "GPU delta within +0.2ms when possible"
                };

                foreach (var action in rule.applyActionsTemplate)
                {
                    wo.applyActions.Add(new USD_ApplyAction
                    {
                        type = action.type,
                        payloadJson = action.payloadJson,
                        requiresUserOptIn = action.requiresUserOptIn
                    });
                }

                var severityWeight = USD_TastePolicyUtil.SeverityWeight(tastePolicy, wo.severity);
                var categoryWeight = USD_TastePolicyUtil.CategoryWeight(tastePolicy, wo.subCategory);
                var learnedBoost = string.IsNullOrEmpty(learnedHint) ? 0f : 0.1f;
                wo.sortScore = severityWeight * categoryWeight + learnedBoost;

                USD_AdviceLocalizer.NormalizeWorkOrder(wo);
                orders.Add(wo);
            }

            orders = orders
                .OrderByDescending(x => x.sortScore)
                .ThenBy(x => USD_TastePolicyUtil.PriorityOrderIndex(tastePolicy, x.subCategory))
                .ThenBy(x => x.id)
                .ToList();

            return orders;
        }

        private static List<string> BuildEvidence(USD_ScanSnapshot snapshot, USD_Rule rule)
        {
            var evidence = new List<string> { $"Rule {rule.id} matched with {rule.triggers.Count} trigger(s)." };
            foreach (var trigger in rule.triggers)
            {
                evidence.Add($"{trigger.field}={ResolveField(snapshot, trigger.field)} ({trigger.op} {trigger.value})");
            }

            evidence.Add($"GlobalVolume={snapshot.hasGlobalVolume}, Overrides={snapshot.enabledOverrides.Count}, ReflectionProbes={snapshot.reflectionProbeCount}");
            evidence.Add($"RendererCount={snapshot.rendererCount}, ShaderCount={snapshot.shaderCountDistinct}, TransparentCount={snapshot.transparentRendererCount}");
            return evidence;
        }

        private static bool MatchAll(USD_ScanSnapshot snapshot, List<USD_Trigger> triggers)
        {
            if (triggers == null || triggers.Count == 0) return true;
            foreach (var trigger in triggers)
            {
                if (!Match(snapshot, trigger)) return false;
            }

            return true;
        }

        private static bool Match(USD_ScanSnapshot snapshot, USD_Trigger trigger)
        {
            var left = ResolveField(snapshot, trigger.field);
            var right = trigger.value;
            switch (trigger.op)
            {
                case "==": return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
                case "!=": return !string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
                case ">": return ToFloat(left) > ToFloat(right);
                case "<": return ToFloat(left) < ToFloat(right);
                case "contains": return left != null && left.IndexOf(right, StringComparison.OrdinalIgnoreCase) >= 0;
                case "not_contains": return left == null || left.IndexOf(right, StringComparison.OrdinalIgnoreCase) < 0;
                default: return false;
            }
        }

        private static string ResolveField(USD_ScanSnapshot s, string field)
        {
            switch (field)
            {
                case "Lighting.hasDirectionalLight": return s.hasDirectionalLight.ToString();
                case "Lighting.dirLightShadowsEnabled": return s.dirLightShadowsEnabled.ToString();
                case "Lighting.ambientIntensity": return s.ambientIntensity.ToString(CultureInfo.InvariantCulture);
                case "Lighting.hasSkyboxMaterial": return s.hasSkyboxMaterial.ToString();
                case "Lighting.hasAnyReflectionProbe": return s.hasAnyReflectionProbe.ToString();
                case "Volumes.hasGlobalVolume": return s.hasGlobalVolume.ToString();
                case "Volumes.enabledOverridesCount": return s.enabledOverrides.Count.ToString();
                case "Volumes.enabledOverrides": return string.Join(",", s.enabledOverrides);
                case "Scene.shaderCountDistinct": return s.shaderCountDistinct.ToString();
                case "Scene.materialCountDistinct": return s.materialCountDistinct.ToString();
                case "Scene.transparentRendererCount": return s.transparentRendererCount.ToString();
                case "URP.shadowDistance": return s.shadowDistance.ToString(CultureInfo.InvariantCulture);
                case "URP.shadowCascadeCount": return s.shadowCascadeCount.ToString();
                case "URP.msaaSampleCount": return s.msaaSampleCount.ToString();
                case "URP.supportsHDR": return s.supportsHDR.ToString();
                case "URP.renderScale": return s.renderScale.ToString(CultureInfo.InvariantCulture);
                case "URP.additionalLightsEnabled": return s.additionalLightsEnabled.ToString();
                default: return string.Empty;
            }
        }

        private static float ToFloat(string val)
        {
            float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var f);
            return f;
        }
    }
}
