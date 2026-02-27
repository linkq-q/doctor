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
            result.WorkOrders.AddRange(USD_RuleEngine.Evaluate(snapshot, rulePack, deltaPatch));
            result.Warnings.AddRange(snapshot.warnings);

            if (context.Mode == USD_RunMode.Apply)
            {
                result.AppliedChanges.AddRange(USD_PatchApplier.ApplySafeAtmospherePatch(context.SceneName, context.Timestamp, context.AllowModifyExistingAssets));
            }

            return result;
        }
    }
}
