using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    [System.Serializable]
    public sealed class USD_TasteActionNote
    {
        public string knob;
        public string before;
        public string after;
        public string intent;
        public string stop_rule;
    }

    [System.Serializable]
    public sealed class USD_TasteNoteTemplate
    {
        public string scene;
        public string[] tags;
        public string goal_mood;
        public string[] before_issues;
        public USD_TasteActionNote[] actions;
        public string after_evaluation;
    }

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
            File.WriteAllText(root + "/snapshot_before.json", JsonUtility.ToJson(before, true));
            var beforeShots = USD_ScreenshotUtil.CaptureSixShots(root + "/before", hub.ScreenshotWidth, hub.ScreenshotHeight, _overrideCamera);

            var applyResult = hub.RunAtmosForExternal(ts, USD_RunMode.Apply);
            var afterScan = hub.RunAtmosForExternal(ts + "_after", USD_RunMode.Scan);
            var after = afterScan.Snapshot ?? USD_AtmosScanner.CaptureSnapshot();
            File.WriteAllText(root + "/snapshot_after.json", JsonUtility.ToJson(after, true));
            var afterShots = USD_ScreenshotUtil.CaptureSixShots(root + "/after", hub.ScreenshotWidth, hub.ScreenshotHeight, _overrideCamera);

            var patch = USD_DeltaExtractor.Extract(sceneName, root + "/snapshot_before.json", root + "/snapshot_after.json");
            if (patch != null)
            {
                File.WriteAllText(root + "/deltaPatch.json", JsonUtility.ToJson(patch, true));
            }

            var diff = USD_DiffUtil.BuildDiff(sceneName, ts, before, after, afterScan.WorkOrders, patch);
            File.WriteAllText(root + "/diff.json", JsonUtility.ToJson(diff, true));

            var report = hub.BuildReportFromResult(afterScan, sceneName, ts);
            report.appliedChanges.AddRange(applyResult.AppliedChanges);
            var reportMd = USD_ReportUtil.WriteReport(sceneName, ts, report);
            var reportJson = reportMd.Replace("_report.md", "_report.json");
            File.Copy(reportMd, root + "/report.md", true);
            File.Copy(reportJson, root + "/report.json", true);

            File.WriteAllText(root + "/summary.md", BuildSummary(hub, report, diff, root, beforeShots, afterShots));
            GenerateTasteNoteTemplate(root, sceneName, patch);

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Evidence Pack Created", "Output: " + root, "OK");
            return root;
        }

        private static void GenerateTasteNoteTemplate(string root, string sceneName, USD_DeltaPatch patch)
        {
            var actions = new List<USD_TasteActionNote>();
            if (patch != null)
            {
                foreach (var field in patch.changedFields.Where(x => x.path.StartsWith("VolumeKey.")))
                {
                    actions.Add(new USD_TasteActionNote
                    {
                        knob = field.path,
                        before = field.before,
                        after = field.after,
                        intent = "",
                        stop_rule = ""
                    });
                }
            }

            var note = new USD_TasteNoteTemplate
            {
                scene = sceneName,
                tags = new[] { "mood", "lighting", "learning" },
                goal_mood = "",
                before_issues = new[] { "" },
                actions = actions.ToArray(),
                after_evaluation = ""
            };

            File.WriteAllText(root + "/taste_note.json", JsonUtility.ToJson(note, true));
        }

        private static string BuildSummary(USD_HubWindow hub, USD_Report report, USD_DiffReport diff, string root, List<USD_ShotCapture> beforeShots, List<USD_ShotCapture> afterShots)
        {
            var p0 = report.workOrders.Count(x => x.severity == "P0");
            var p1 = report.workOrders.Count(x => x.severity == "P1");
            var p2 = report.workOrders.Count(x => x.severity == "P2");

            var sb = new StringBuilder();
            sb.AppendLine("# Evidence Pack Summary");
            sb.AppendLine($"- Scene: {report.sceneName}");
            sb.AppendLine($"- Timestamp: {report.timestamp}");
            sb.AppendLine($"- Tool Version: {report.toolVersion}");
            sb.AppendLine($"- Apply Mode: {hub.ApplyMode}");
            sb.AppendLine($"- Style Profile: {(hub.SelectedStyleProfile != null ? hub.SelectedStyleProfile.profileName : "Neutral Baseline")}");
            sb.AppendLine($"- Bind Policy: {(hub.AssignNewProfileToExistingGlobalVolume ? "ON" : "OFF")}");
            sb.AppendLine($"- Matched Rules: P0={p0}, P1={p1}, P2={p2}");

            sb.AppendLine("## Policy Checklist");
            sb.AppendLine($"- Passed: {report.policyPassCount}");
            sb.AppendLine($"- Warnings: {report.policyWarningCount}");
            if (report.policyChecklist != null) report.policyChecklist.ForEach(x => sb.AppendLine("- " + x));

            sb.AppendLine("## Key Evidence");
            sb.AppendLine($"- Global Volume: {report.snapshot.hasGlobalVolume}");
            sb.AppendLine($"- Directional Shadow Enabled: {report.snapshot.dirLightShadowsEnabled}");
            sb.AppendLine($"- Shadow Distance: {report.snapshot.shadowDistance}");
            sb.AppendLine($"- Enabled Overrides: {report.snapshot.enabledOverrides.Count}");
            sb.AppendLine("## Applied Actions");
            report.appliedChanges.ForEach(x => sb.AppendLine("- " + x));

            sb.AppendLine("## Key diff summary (Top 5)");
            foreach (var change in diff.pipelineChanges.Concat(diff.volumeChanges).Concat(diff.sceneChanges).Take(5))
            {
                sb.AppendLine($"- {change.path}: {change.before} -> {change.after}");
            }

            sb.AppendLine("## Repro Camera Shots");
            WriteShotSection(sb, "Before", beforeShots);
            WriteShotSection(sb, "After", afterShots);

            sb.AppendLine("## Output Folder");
            sb.AppendLine("- " + root);
            sb.AppendLine("- taste_note.json template generated");
            return sb.ToString();
        }

        private static void WriteShotSection(StringBuilder sb, string section, List<USD_ShotCapture> shots)
        {
            sb.AppendLine($"### {section}");
            if (shots == null || shots.Count == 0)
            {
                sb.AppendLine("- (no shots captured)");
                return;
            }

            foreach (var shot in shots)
            {
                sb.AppendLine($"- {Path.GetFileName(shot.path)} | pos=({shot.position.x:0.##},{shot.position.y:0.##},{shot.position.z:0.##}) | yaw={shot.yaw:0.##} | pitch={shot.pitch:0.##} | fov={shot.fov:0.##}");
            }
        }
    }
}
