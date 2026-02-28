using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public sealed class USD_TuningModule : IToolModule
    {
        public string ModuleName => "Tuning (Before/After)";

        public string BeforeSnapshotPath { get; private set; }
        public string AfterSnapshotPath { get; private set; }
        public string LastDeltaPatchPath { get; private set; }
        public int LastChangedFieldsCount { get; private set; }
        public string LastPolicyAlignment { get; private set; }

        public void DrawUI(USD_HubWindow hub)
        {
            EditorGUILayout.HelpBox("Capture manual tuning before/after and extract a delta patch.", MessageType.Info);
            if (GUILayout.Button("Capture BEFORE"))
            {
                var snap = USD_AtmosScanner.CaptureSnapshot();
                var ts = USD_EditorUtil.Timestamp;
                BeforeSnapshotPath = USD_SnapshotUtil.SaveSnapshot(hub.ActiveSceneName, ts, snap);
            }

            if (GUILayout.Button("Capture AFTER"))
            {
                var snap = USD_AtmosScanner.CaptureSnapshot();
                var ts = USD_EditorUtil.Timestamp;
                AfterSnapshotPath = USD_SnapshotUtil.SaveSnapshot(hub.ActiveSceneName, ts, snap);
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(BeforeSnapshotPath) || string.IsNullOrEmpty(AfterSnapshotPath)))
            {
                if (GUILayout.Button("Extract Delta Patch"))
                {
                    var patch = USD_DeltaExtractor.Extract(hub.ActiveSceneName, BeforeSnapshotPath, AfterSnapshotPath);
                    if (patch != null)
                    {
                        LastChangedFieldsCount = patch.changedFields.Count;
                        LastDeltaPatchPath = USD_SnapshotUtil.SaveDeltaPatch(hub.ActiveSceneName, USD_EditorUtil.Timestamp, patch);
                        hub.OptionalDeltaPatchPath = LastDeltaPatchPath;
                        var afterSnapshot = USD_SnapshotUtil.LoadSnapshot(AfterSnapshotPath);
                        var policy = hub.ActiveTastePolicy;
                        var check = USD_PolicyChecker.Evaluate(afterSnapshot, policy, hub.PolicyBrightnessBucket);
                        LastPolicyAlignment = $"Pass={check.passed}, Warnings={check.warnings}";
                    }
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Snapshot Folder"))
            {
                var folder = USD_EditorUtil.EnsureSceneSubFolder("Snapshots", hub.ActiveSceneName);
                EditorUtility.RevealInFinder(folder);
            }

            if (GUILayout.Button("Open Patch Folder"))
            {
                var folder = USD_EditorUtil.EnsureSceneSubFolder("Patches", hub.ActiveSceneName);
                EditorUtility.RevealInFinder(folder);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Before Snapshot:", BeforeSnapshotPath ?? "(none)");
            EditorGUILayout.LabelField("After Snapshot:", AfterSnapshotPath ?? "(none)");
            EditorGUILayout.LabelField("Delta Patch:", LastDeltaPatchPath ?? "(none)");
            EditorGUILayout.LabelField("Delta Summary:", $"changed fields = {LastChangedFieldsCount}");
            EditorGUILayout.LabelField("Policy Alignment:", string.IsNullOrEmpty(LastPolicyAlignment) ? "(not evaluated)" : LastPolicyAlignment);
        }

        public USD_ModuleResult Execute(USD_RunContext context)
        {
            return new USD_ModuleResult { ModuleName = ModuleName, Snapshot = context.Snapshot ?? USD_AtmosScanner.CaptureSnapshot() };
        }
    }
}
