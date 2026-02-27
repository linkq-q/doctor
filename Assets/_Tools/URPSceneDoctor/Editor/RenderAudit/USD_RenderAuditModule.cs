using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public sealed class USD_RenderAuditModule : IToolModule
    {
        public string ModuleName => "Render Doctor";

        public void DrawUI(USD_HubWindow hub)
        {
            EditorGUILayout.LabelField("Lightweight render performance advisory checks.", EditorStyles.wordWrappedLabel);
            hub.DrawExecutionButtons(this);
        }

        public USD_ModuleResult Execute(USD_RunContext context)
        {
            var result = new USD_ModuleResult { ModuleName = ModuleName };
            var s = context.Snapshot ?? USD_RenderScanner.CaptureSnapshot();
            result.Snapshot = s;

            if (s.transparentRendererCount > 80)
                result.WorkOrders.Add(Simple("RD-01", "透明物体过多", "P1", "Render", $"transparentRendererCount={s.transparentRendererCount}"));
            if (s.materialCountDistinct > 120 || s.shaderCountDistinct > 40)
                result.WorkOrders.Add(Simple("RD-02", "材质/Shader 过散", "P1", "Render", $"materials={s.materialCountDistinct}, shaders={s.shaderCountDistinct}"));
            if (s.shadowDistance > 70 && s.shadowCascadeCount >= 4)
                result.WorkOrders.Add(Simple("RD-03", "阴影距离/级联偏高", "P1", "Render", $"shadowDistance={s.shadowDistance}, cascades={s.shadowCascadeCount}"));
            if (s.msaaSampleCount >= 4 || s.supportsHDR || s.renderScale > 1.0f)
                result.WorkOrders.Add(Simple("RD-04", "MSAA/HDR/RenderScale 风险", "P2", "Render", $"msaa={s.msaaSampleCount}, hdr={s.supportsHDR}, scale={s.renderScale}"));
            if (s.additionalLightsEnabled && s.additionalLightsPerObjectLimit > 4)
                result.WorkOrders.Add(Simple("RD-05", "额外灯光风险", "P1", "Render", $"additionalLightsPerObject={s.additionalLightsPerObjectLimit}"));
            if (s.enabledOverrides.Count > 6)
                result.WorkOrders.Add(Simple("RD-06", "后处理堆叠过多", "P2", "Render", $"enabledOverrides={s.enabledOverrides.Count}"));

            result.Warnings.AddRange(s.warnings);
            return result;
        }

        private static USD_WorkOrder Simple(string id, string title, string severity, string category, string evidence)
        {
            var wo = new USD_WorkOrder
            {
                id = id,
                title = title,
                severity = severity,
                category = category,
                diagnosis = "Evidence-driven lightweight render risk detected.",
                costNotes = "Low-medium"
            };
            wo.symptoms.Add(title);
            wo.evidence.Add(evidence);
            wo.prescriptions.Add(new USD_Prescription
            {
                priority = 1,
                actionText = "Review and reduce the highest offender first.",
                targetPathHint = "URP Asset / Scene Renderers",
                recommendedRange = "Use platform-appropriate budgets and test on target hardware"
            });
            wo.verification.steps.Add("Capture frame timing before/after and compare GPU/CPU frame time.");
            wo.verification.performanceBudgetHint = "No regression over baseline.";
            return wo;
        }
    }
}
