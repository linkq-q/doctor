using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public sealed class USD_AtmosAuditModule : IToolModule
    {
        public string ModuleName => "Atmosphere Doctor";

        public void DrawUI(USD_HubWindow hub)
        {
            EditorGUILayout.LabelField("Atmosphere-first audit based on project evidence.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Taste Policy:", hub.ActiveTastePolicy != null ? hub.ActiveTastePolicy.policyName : "(none)");
            if (GUILayout.Button("Create Evidence Pack"))
            {
                hub.CreateEvidencePack();
            }
            hub.DrawExecutionButtons(this);
        }

        public USD_ModuleResult Execute(USD_RunContext context)
        {
            var result = new USD_ModuleResult { ModuleName = ModuleName };
            var snapshot = context.Snapshot ?? USD_AtmosScanner.CaptureSnapshot();
            result.Snapshot = snapshot;

            var rulePack = USD_RuleEngine.LoadRulePack(context.SelectedRulePackPath);
            var deltaPatch = string.IsNullOrEmpty(context.OptionalDeltaPatchPath)
                ? null
                : USD_SnapshotUtil.LoadDeltaPatch(context.OptionalDeltaPatchPath);
            result.WorkOrders.AddRange(USD_RuleEngine.Evaluate(snapshot, rulePack, deltaPatch, context.TastePolicy, context.LearningStats));
            result.Warnings.AddRange(snapshot.warnings);
            AppendPipelinePackRecommendations(result, snapshot);

            if (context.Mode == USD_RunMode.Apply)
            {
                result.AppliedChanges.AddRange(USD_PatchApplier.ApplyPatch(context.SceneName, context.Timestamp, context.ApplyMode, context.StyleProfile, context.AllowModifyExistingAssets));
            }

            return result;
        }


        private static void AppendPipelinePackRecommendations(USD_ModuleResult result, USD_ScanSnapshot snapshot)
        {
            var preflight = USD_PipelinePackUtil.RunPreflight();
            if (!preflight.checks.Exists(x => x.id == "fog.feature" && x.level == USD_CheckLevel.Pass))
            {
                result.WorkOrders.Add(new USD_WorkOrder
                {
                    id = "USD-PACK-FOG-001",
                    title = "Consider enabling Distance Fog (Pipeline Base Pack v1)",
                    severity = "P1",
                    category = "Atmosphere",
                    subCategory = "Fog",
                    diagnosis = "Distance Fog is not installed; depth layering may feel flat in mid/far distance.",
                    sortScore = 0.9f,
                    symptoms = { "Distant geometry separation is weak", "No distance haze control from pipeline pack" },
                    evidence = { "Pipeline Preflight: Distance Fog feature missing." },
                    prescriptions = { new USD_Prescription { priority = 1, actionText = "Open Pipeline Pack tab and run Preflight -> Install Base Pack v1", targetPathHint = "Pipeline Pack", recommendedRange = "Enable safe default intensity first" } },
                    verification = new USD_Verification { steps = { "Create BEFORE/AFTER evidence pack from Pipeline Pack page" }, performanceBudgetHint = "Fog can increase overdraw and bandwidth." }
                });
            }

            if (!preflight.checks.Exists(x => x.id == "vol.feature" && x.level == USD_CheckLevel.Pass) && snapshot.hasDirectionalLight)
            {
                result.WorkOrders.Add(new USD_WorkOrder
                {
                    id = "USD-PACK-VOL-001",
                    title = "Consider enabling Volumetric Light for main directional light",
                    severity = "P1",
                    category = "Atmosphere",
                    subCategory = "Volumetric",
                    diagnosis = "Main light exists but volumetric layer is not installed.",
                    sortScore = 0.85f,
                    symptoms = { "Sun shafts/god rays absent", "Atmospheric depth layering can be improved" },
                    evidence = { "Pipeline Preflight: Volumetric Light feature missing." },
                    prescriptions = { new USD_Prescription { priority = 1, actionText = "Install Base Pack v1 in Pipeline Pack", targetPathHint = "Pipeline Pack", recommendedRange = "Start with low intensity and verify in Evidence Pack" } },
                    verification = new USD_Verification { steps = { "Compare BEFORE/AFTER in bright directional lighting" }, performanceBudgetHint = "Volumetrics may add GPU cost; validate target platform." }
                });
            }

            result.WorkOrders.Sort((a, b) => b.sortScore.CompareTo(a.sortScore));
        }

    }
}
