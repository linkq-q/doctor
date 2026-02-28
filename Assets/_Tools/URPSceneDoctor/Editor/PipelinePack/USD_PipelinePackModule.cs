using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public sealed class USD_PipelinePackModule : IToolModule
    {
        public string ModuleName => "Pipeline Pack";
        private USD_PipelinePreflightResult _lastPreflight;
        private Vector2 _scroll;

        public void DrawUI(USD_HubWindow hub)
        {
            EditorGUILayout.HelpBox(USD_Loc.T("pipeline.overview"), MessageType.Info);
            EditorGUILayout.LabelField(USD_Loc.T("pipeline.pack"), "Base Pack v1");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(USD_Loc.C("pipeline.preflight")))
            {
                _lastPreflight = USD_PipelinePackUtil.RunPreflight();
                WritePreflightReport(hub, _lastPreflight);
            }

            if (GUILayout.Button(USD_Loc.C("pipeline.install")))
            {
                var ts = USD_EditorUtil.Timestamp;
                var sceneName = string.IsNullOrWhiteSpace(hub.ActiveSceneName) ? "UntitledScene" : hub.ActiveSceneName;
                var changes = USD_PipelinePackUtil.InstallBasePackV1(sceneName, ts);
                WriteActionReport(sceneName, ts, "Install", changes);
            }

            if (GUILayout.Button(USD_Loc.C("pipeline.uninstall")))
            {
                var ts = USD_EditorUtil.Timestamp;
                var sceneName = string.IsNullOrWhiteSpace(hub.ActiveSceneName) ? "UntitledScene" : hub.ActiveSceneName;
                var changes = USD_PipelinePackUtil.UninstallBasePackV1();
                WriteActionReport(sceneName, ts, "Uninstall", changes);
            }

            if (GUILayout.Button(USD_Loc.C("pipeline.evidence")))
            {
                CreatePipelineEvidencePack(hub);
            }
            EditorGUILayout.EndHorizontal();

            if (_lastPreflight != null)
            {
                DrawPreflightStatus(_lastPreflight);
            }
        }

        public USD_ModuleResult Execute(USD_RunContext context)
        {
            var r = new USD_ModuleResult { ModuleName = ModuleName, Snapshot = context.Snapshot ?? USD_AtmosScanner.CaptureSnapshot() };
            var preflight = USD_PipelinePackUtil.RunPreflight();
            foreach (var c in preflight.checks)
            {
                if (c.level == USD_CheckLevel.Fail)
                {
                    r.Warnings.Add("[P0] " + c.title + ": " + c.detail);
                }
                else if (c.level == USD_CheckLevel.Warning)
                {
                    r.Warnings.Add("[P1] " + c.title + ": " + c.detail);
                }
            }
            return r;
        }

        private void DrawPreflightStatus(USD_PipelinePreflightResult result)
        {
            var summaryType = result.P0Count > 0 ? MessageType.Error : (result.P1Count > 0 ? MessageType.Warning : MessageType.Info);
            EditorGUILayout.HelpBox($"Installed={result.installed} | URP={result.urpVersion} | P0={result.P0Count}, P1={result.P1Count}", summaryType);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(200));
            foreach (var c in result.checks)
            {
                var icon = c.level == USD_CheckLevel.Pass ? "🟢" : c.level == USD_CheckLevel.Warning ? "🟡" : "🔴";
                EditorGUILayout.LabelField($"{icon} {USD_AdviceLocalizer.NormalizeSentence(c.title)}: {USD_AdviceLocalizer.NormalizeSentence(c.detail)}", EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private static void WritePreflightReport(USD_HubWindow hub, USD_PipelinePreflightResult result)
        {
            var sceneName = string.IsNullOrWhiteSpace(hub.ActiveSceneName) ? "UntitledScene" : hub.ActiveSceneName;
            var ts = USD_EditorUtil.Timestamp;
            var reportDir = USD_EditorUtil.EnsureSceneSubFolder("Reports", sceneName);
            var path = $"{reportDir}/{ts}_pipeline_preflight.md";
            var sb = new StringBuilder();
            sb.AppendLine("# Pipeline Pack Preflight");
            sb.AppendLine($"- Pack: {result.packName}");
            sb.AppendLine($"- URP Version: {result.urpVersion}");
            sb.AppendLine($"- Installed: {result.installed}");
            foreach (var c in result.checks)
            {
                sb.AppendLine($"- [{c.level}] {USD_AdviceLocalizer.NormalizeSentence(c.title)}: {USD_AdviceLocalizer.NormalizeSentence(c.detail)}");
            }
            File.WriteAllText(path, sb.ToString());
            AssetDatabase.Refresh();
        }

        private static void WriteActionReport(string sceneName, string timestamp, string action, System.Collections.Generic.List<string> changes)
        {
            var reportDir = USD_EditorUtil.EnsureSceneSubFolder("Reports", sceneName);
            var path = $"{reportDir}/{timestamp}_pipeline_{action.ToLowerInvariant()}.md";
            var sb = new StringBuilder();
            sb.AppendLine("# Pipeline Pack " + action);
            changes.ForEach(c => sb.AppendLine("- " + USD_AdviceLocalizer.NormalizeSentence(c)));
            File.WriteAllText(path, sb.ToString());
            AssetDatabase.Refresh();
        }

        private static void CreatePipelineEvidencePack(USD_HubWindow hub)
        {
            var sceneName = string.IsNullOrWhiteSpace(hub.ActiveSceneName) ? "UntitledScene" : hub.ActiveSceneName;
            var ts = USD_EditorUtil.Timestamp;
            var root = $"Assets/_Tools/URPSceneDoctor/EvidencePacks/{sceneName}/{ts}_PipelinePack";
            USD_EditorUtil.EnsureFolder("Assets/_Tools/URPSceneDoctor/EvidencePacks");
            USD_EditorUtil.EnsureFolder($"Assets/_Tools/URPSceneDoctor/EvidencePacks/{sceneName}");
            USD_EditorUtil.EnsureFolder(root);
            USD_EditorUtil.EnsureFolder(root + "/before");
            USD_EditorUtil.EnsureFolder(root + "/after");

            var before = USD_AtmosScanner.CaptureSnapshot();
            File.WriteAllText(root + "/snapshot_before.json", JsonUtility.ToJson(before, true));
            var beforeShots = USD_ScreenshotUtil.CaptureSixShots(root + "/before", hub.ScreenshotWidth, hub.ScreenshotHeight);

            var changes = USD_PipelinePackUtil.InstallBasePackV1(sceneName, ts);

            var after = USD_AtmosScanner.CaptureSnapshot();
            File.WriteAllText(root + "/snapshot_after.json", JsonUtility.ToJson(after, true));
            var afterShots = USD_ScreenshotUtil.CaptureSixShots(root + "/after", hub.ScreenshotWidth, hub.ScreenshotHeight);

            var diff = USD_DiffUtil.BuildDiff(sceneName, ts, before, after, new System.Collections.Generic.List<USD_WorkOrder>(), null);
            File.WriteAllText(root + "/diff.json", JsonUtility.ToJson(diff, true));

            var summary = new StringBuilder();
            summary.AppendLine("# Pipeline Pack Evidence");
            summary.AppendLine("## Pack changes");
            changes.ForEach(c => summary.AppendLine("- " + USD_AdviceLocalizer.NormalizeSentence(c)));
            summary.AppendLine("## Shot counts");
            summary.AppendLine($"- BEFORE: {beforeShots.Count}");
            summary.AppendLine($"- AFTER: {afterShots.Count}");
            var topDiff = diff.pipelineChanges.Concat(diff.volumeChanges).Concat(diff.sceneChanges).Take(5).ToList();
            summary.AppendLine("## Top Diff");
            if (topDiff.Count == 0) summary.AppendLine("- (none)");
            topDiff.ForEach(d => summary.AppendLine($"- {d.path}: {d.before} -> {d.after}"));
            File.WriteAllText(root + "/summary.md", summary.ToString());

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(USD_Loc.T("pipeline.evidence"), USD_Loc.T("common.outputFmt", root), USD_Loc.T("common.ok"));
        }
    }
}
