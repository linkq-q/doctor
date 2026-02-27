using System.IO;
using System.Text;
using UnityEditor;

namespace URPSceneDoctor.Editor
{
    public static class USD_ReportUtil
    {
        public static void WriteReport(string sceneName, string timestamp, USD_Report report)
        {
            var folder = USD_EditorUtil.EnsureSceneSubFolder("Reports", sceneName);
            var jsonPath = $"{folder}/{timestamp}_report.json";
            var mdPath = $"{folder}/{timestamp}_report.md";

            File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true));
            File.WriteAllText(mdPath, BuildMarkdown(report));
            AssetDatabase.Refresh();
        }

        private static string BuildMarkdown(USD_Report report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# URP Scene Doctor Report");
            sb.AppendLine("- Project Info");
            sb.AppendLine($"  - Unity: {report.snapshot.unityVersion}");
            sb.AppendLine($"  - URP: {report.snapshot.urpPackageVersion}");
            sb.AppendLine($"  - Scene: {report.sceneName}");
            sb.AppendLine("- Snapshot Summary (key fields)");
            sb.AppendLine($"  - Global Volume: {report.snapshot.hasGlobalVolume}");
            sb.AppendLine($"  - Renderers: {report.snapshot.rendererCount} (Transparent: {report.snapshot.transparentRendererCount})");
            sb.AppendLine($"  - Shadow Distance: {report.snapshot.shadowDistance}");
            sb.AppendLine("- Work Orders");

            foreach (var wo in report.workOrders)
            {
                sb.AppendLine($"## [{wo.id}] {wo.title} ({wo.severity})");
                sb.AppendLine("### Symptoms");
                wo.symptoms.ForEach(x => sb.AppendLine("- " + x));
                sb.AppendLine("### Evidence");
                wo.evidence.ForEach(x => sb.AppendLine("- " + x));
                sb.AppendLine("### Diagnosis");
                sb.AppendLine(wo.diagnosis);
                sb.AppendLine("### Prescriptions");
                foreach (var p in wo.prescriptions)
                {
                    sb.AppendLine($"- P{p.priority}: {p.actionText} | {p.targetPathHint} | {p.recommendedRange}");
                }

                sb.AppendLine("### Verification");
                wo.verification.steps.ForEach(x => sb.AppendLine("- " + x));
                sb.AppendLine($"Performance budget hint: {wo.verification.performanceBudgetHint}");
                sb.AppendLine("### Cost");
                sb.AppendLine(wo.costNotes);
                sb.AppendLine("### ApplyActions");
                wo.applyActions.ForEach(x => sb.AppendLine($"- {x.type}, opt-in: {x.requiresUserOptIn}"));
            }

            sb.AppendLine("- Applied Changes (if any)");
            report.appliedChanges.ForEach(c => sb.AppendLine("  - " + c));
            sb.AppendLine("- Warnings");
            report.warnings.ForEach(w => sb.AppendLine("  - " + w));
            return sb.ToString();
        }
    }
}
