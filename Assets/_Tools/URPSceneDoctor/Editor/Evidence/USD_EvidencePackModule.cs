using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public sealed class USD_EvidencePackModule : IToolModule
    {
        public string ModuleName => "Evidence Pack";
        private Camera _overrideCamera;

        public void DrawUI(USD_HubWindow hub)
        {
            EditorGUILayout.HelpBox("Generate shareable evidence pack: before/after shots + summary + diff.", MessageType.Info);
            _overrideCamera = (Camera)EditorGUILayout.ObjectField("Optional Camera (Mode B)", _overrideCamera, typeof(Camera), true);
            if (GUILayout.Button("Create Evidence Pack"))
            {
                CreatePack(hub);
            }
        }

        public USD_ModuleResult Execute(USD_RunContext context)
        {
            return new USD_ModuleResult { ModuleName = ModuleName, Snapshot = context.Snapshot ?? USD_AtmosScanner.CaptureSnapshot() };
        }

        public string CreatePack(USD_HubWindow hub)
        {
            var sceneName = string.IsNullOrWhiteSpace(hub.ActiveSceneName) ? "UntitledScene" : hub.ActiveSceneName;
            var ts = USD_EditorUtil.Timestamp;

            var root = $"Assets/_Tools/URPSceneDoctor/EvidencePacks/{sceneName}/{ts}";
            USD_EditorUtil.EnsureFolder("Assets/_Tools/URPSceneDoctor/EvidencePacks");
            USD_EditorUtil.EnsureFolder($"Assets/_Tools/URPSceneDoctor/EvidencePacks/{sceneName}");
            USD_EditorUtil.EnsureFolder(root);
            USD_EditorUtil.EnsureFolder(root + "/before");
            USD_EditorUtil.EnsureFolder(root + "/after");

            var before = USD_AtmosScanner.CaptureSnapshot();
            var beforePath = root + "/snapshot_before.json";
            File.WriteAllText(beforePath, JsonUtility.ToJson(before, true));
            USD_ScreenshotUtil.CaptureSixShots(root + "/before", hub.ScreenshotWidth, hub.ScreenshotHeight, _overrideCamera);

            var runResult = hub.RunAtmosScanForExternal(ts);
            var after = runResult.Snapshot ?? USD_AtmosScanner.CaptureSnapshot();
            var afterPath = root + "/snapshot_after.json";
            File.WriteAllText(afterPath, JsonUtility.ToJson(after, true));
            USD_ScreenshotUtil.CaptureSixShots(root + "/after", hub.ScreenshotWidth, hub.ScreenshotHeight, _overrideCamera);

            var patch = string.IsNullOrEmpty(hub.OptionalDeltaPatchPath) ? null : USD_SnapshotUtil.LoadDeltaPatch(hub.OptionalDeltaPatchPath);
            var diff = USD_DiffUtil.BuildDiff(sceneName, ts, before, after, runResult.WorkOrders, patch);
            File.WriteAllText(root + "/diff.json", JsonUtility.ToJson(diff, true));

            var report = hub.BuildReportFromResult(runResult, sceneName, ts);
            var reportMd = USD_ReportUtil.WriteReport(sceneName, ts, report);
            var reportJson = reportMd.Replace("_report.md", "_report.json");
            File.Copy(reportMd, root + "/report.md", true);
            File.Copy(reportJson, root + "/report.json", true);

            if (patch != null)
            {
                File.Copy(hub.OptionalDeltaPatchPath, root + "/deltaPatch.json", true);
            }

            File.WriteAllText(root + "/summary.md", BuildSummary(hub, report, diff, root));
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Evidence Pack Created", "Output: " + root, "OK");
            return root;
        }

        private static string BuildSummary(USD_HubWindow hub, USD_Report report, USD_DiffReport diff, string root)
        {
            var p0 = report.workOrders.Count(x => x.severity == "P0");
            var p1 = report.workOrders.Count(x => x.severity == "P1");
            var p2 = report.workOrders.Count(x => x.severity == "P2");

            var sb = new StringBuilder();
            sb.AppendLine("# Evidence Pack Summary");
            sb.AppendLine($"- Scene: {report.sceneName}");
            sb.AppendLine($"- Timestamp: {report.timestamp}");
            sb.AppendLine($"- Tool Version: {report.toolVersion}");
            sb.AppendLine($"- Matched Rules: P0={p0}, P1={p1}, P2={p2}");
            sb.AppendLine("## Key Evidence");
            sb.AppendLine($"- Global Volume: {report.snapshot.hasGlobalVolume}");
            sb.AppendLine($"- Directional Shadow Enabled: {report.snapshot.dirLightShadowsEnabled}");
            sb.AppendLine($"- Shadow Distance: {report.snapshot.shadowDistance}");
            sb.AppendLine($"- Enabled Overrides: {report.snapshot.enabledOverrides.Count}");
            sb.AppendLine("## Applied Actions");
            report.appliedChanges.ForEach(x => sb.AppendLine("- " + x));
            sb.AppendLine("## Personal Learned Hints");
            report.personalDeltaHints.Take(5).ToList().ForEach(x => sb.AppendLine("- " + x));
            if (report.personalDeltaHints.Count == 0) sb.AppendLine("- (none)");
            sb.AppendLine("## Output Folder");
            sb.AppendLine("- " + root);
            return sb.ToString();
        }
    }
}
