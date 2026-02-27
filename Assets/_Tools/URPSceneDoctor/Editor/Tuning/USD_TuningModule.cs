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
                        LastDeltaPatchPath = USD_SnapshotUtil.SaveDeltaPatch(hub.ActiveSceneName, USD_EditorUtil.Timestamp, patch);
                        hub.OptionalDeltaPatchPath = LastDeltaPatchPath;
                    }
                }
            }

            EditorGUILayout.LabelField("Before:", BeforeSnapshotPath ?? "(none)");
            EditorGUILayout.LabelField("After:", AfterSnapshotPath ?? "(none)");
            EditorGUILayout.LabelField("Delta Patch:", LastDeltaPatchPath ?? "(none)");
        }

        public USD_ModuleResult Execute(USD_RunContext context)
        {
            return new USD_ModuleResult { ModuleName = ModuleName, Snapshot = context.Snapshot ?? USD_AtmosScanner.CaptureSnapshot() };
        }
    }
}
