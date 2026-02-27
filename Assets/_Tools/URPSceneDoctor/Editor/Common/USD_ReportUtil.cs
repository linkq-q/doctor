using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public static class USD_ReportUtil
    {
        public static string WriteReport(string sceneName, string timestamp, USD_Report report)
        {
            var folder = USD_EditorUtil.EnsureSceneSubFolder("Reports", sceneName);
            var jsonPath = $"{folder}/{timestamp}_report.json";
            var mdPath = $"{folder}/{timestamp}_report.md";

            File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true));
            File.WriteAllText(mdPath, BuildMarkdown(report));
            AssetDatabase.Refresh();
            return mdPath;
        }

        private static string BuildMarkdown(USD_Report report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# URP Scene Doctor Report");
            sb.AppendLine($"- Tool Version: {report.toolVersion}");
            sb.AppendLine("- Project Info");
            sb.AppendLine($"  - Unity: {report.snapshot.unityVersion}");
            sb.AppendLine($"  - URP: {report.snapshot.urpPackageVersion}");
            sb.AppendLine($"  - Scene: {report.sceneName}");
            sb.AppendLine("- Snapshot Summary (key fields)");
            sb.AppendLine($"  - Global Volume: {report.snapshot.hasGlobalVolume}");
            sb.AppendLine($"  - Renderers: {report.snapshot.rendererCount} (Transparent: {report.snapshot.transparentRendererCount})");
            sb.AppendLine($"  - Shadow Distance: {report.snapshot.shadowDistance}");
            if (!string.IsNullOrEmpty(report.learningSummary)) sb.AppendLine($"  - Learning: {report.learningSummary}");

            sb.AppendLine($"- Taste Policy Applied: {report.tastePolicyName}");
            if (report.tastePriorityOrder != null && report.tastePriorityOrder.Count > 0)
                sb.AppendLine("  - Priority Order: " + string.Join(" > ", report.tastePriorityOrder));
            if (report.tasteForbiddenActions != null && report.tasteForbiddenActions.Count > 0)
                sb.AppendLine("  - Forbidden: " + string.Join(", ", report.tasteForbiddenActions));

            sb.AppendLine("- Work Orders");
            WriteSeverityGroup(sb, report.workOrders, "P0");
            WriteSeverityGroup(sb, report.workOrders, "P1");
            WriteSeverityGroup(sb, report.workOrders, "P2");

            sb.AppendLine("- Personal Delta Hints");
            if (report.personalDeltaHints == null || report.personalDeltaHints.Count == 0)
            {
                sb.AppendLine("  - (none)");
            }
            else
            {
                report.personalDeltaHints.ForEach(x => sb.AppendLine("  - " + x));
            }

            sb.AppendLine("- Applied Changes (if any)");
            report.appliedChanges.ForEach(c => sb.AppendLine("  - " + c));
            sb.AppendLine("- Warnings");
            report.warnings.ForEach(w => sb.AppendLine("  - " + w));
            return sb.ToString();
        }

        private static void WriteSeverityGroup(StringBuilder sb, List<USD_WorkOrder> allOrders, string severity)
        {
            sb.AppendLine($"## Severity {severity}");
            var orders = allOrders.Where(x => x.severity == severity).ToList();
            if (orders.Count == 0)
            {
                sb.AppendLine("- (none)");
                return;
            }

            foreach (var wo in orders)
            {
                sb.AppendLine($"### [{wo.id}] {wo.title}");
                sb.AppendLine("#### Symptoms");
                wo.symptoms.ForEach(x => sb.AppendLine("- " + x));
                sb.AppendLine("#### Evidence");
                wo.evidence.ForEach(x => sb.AppendLine("- " + x));
                sb.AppendLine("#### Prescription");
                foreach (var p in wo.prescriptions)
                {
                    sb.AppendLine($"- Priority {p.priority}: {p.actionText} | {p.targetPathHint} | {p.recommendedRange}");
                }

                sb.AppendLine("#### Verification");
                wo.verification.steps.ForEach(x => sb.AppendLine("- " + x));
                wo.verification.referenceShots.ForEach(x => sb.AppendLine("- Shot: " + x));
                sb.AppendLine($"- Performance: {wo.verification.performanceBudgetHint}");
                sb.AppendLine("#### Cost");
                sb.AppendLine(wo.costNotes);
                sb.AppendLine("#### ApplyActions");
                wo.applyActions.ForEach(x => sb.AppendLine($"- {x.type}, opt-in: {x.requiresUserOptIn}"));
            }
        }
    }
}
