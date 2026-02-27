using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace URPSceneDoctor.Editor
{
    public sealed class USD_HubWindow : EditorWindow
    {
        private readonly string[] _tabs = { "Atmosphere Doctor", "Render Doctor", "Tuning (Before/After)", "Reports", "Settings" };
        private int _selectedTab;
        private USD_AtmosAuditModule _atmosModule;
        private USD_RenderAuditModule _renderModule;
        private USD_TuningModule _tuningModule;
        private USD_ModuleResult _lastResult;

        public string OptionalDeltaPatchPath;

        public string ActiveSceneName => SceneManager.GetActiveScene().name;

        [MenuItem("Tools/URP Scene Doctor")]
        public static void Open()
        {
            GetWindow<USD_HubWindow>("URP Scene Doctor");
        }

        private void OnEnable()
        {
            _atmosModule = new USD_AtmosAuditModule();
            _renderModule = new USD_RenderAuditModule();
            _tuningModule = new USD_TuningModule();
            USD_Settings.GetOrCreateSettings();
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.BeginHorizontal();
            DrawSidebar();
            DrawContent();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            var snapshot = USD_AtmosScanner.CaptureSnapshot();
            EditorGUILayout.HelpBox($"Scene: {ActiveSceneName} | URP: {snapshot.activeURPAssetName} | Renderer: {snapshot.activeRendererDataName} | Global Volume: {snapshot.hasGlobalVolume}", MessageType.None);
        }

        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(220));
            _selectedTab = GUILayout.SelectionGrid(_selectedTab, _tabs, 1);
            EditorGUILayout.EndVertical();
        }

        private void DrawContent()
        {
            EditorGUILayout.BeginVertical();
            switch (_selectedTab)
            {
                case 0:
                    _atmosModule.DrawUI(this);
                    break;
                case 1:
                    _renderModule.DrawUI(this);
                    break;
                case 2:
                    _tuningModule.DrawUI(this);
                    break;
                case 3:
                    DrawReports();
                    break;
                case 4:
                    DrawSettings();
                    break;
            }

            if (_lastResult != null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Last Work Orders", EditorStyles.boldLabel);
                foreach (var wo in _lastResult.WorkOrders)
                {
                    EditorGUILayout.LabelField($"[{wo.id}] {wo.title} ({wo.severity})");
                }
            }

            EditorGUILayout.EndVertical();
        }

        public void DrawExecutionButtons(IToolModule module)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan")) RunModule(module, USD_RunMode.Scan);
            if (GUILayout.Button("Dry Run")) RunModule(module, USD_RunMode.DryRun);
            if (GUILayout.Button("Apply")) RunModule(module, USD_RunMode.Apply);
            if (GUILayout.Button("Export Report") && _lastResult != null) ExportLastReport();
            EditorGUILayout.EndHorizontal();
        }

        private void RunModule(IToolModule module, USD_RunMode mode)
        {
            var timestamp = USD_EditorUtil.Timestamp;
            var settings = USD_Settings.GetOrCreateSettings();
            var ctx = new USD_RunContext
            {
                Mode = mode,
                Snapshot = USD_AtmosScanner.CaptureSnapshot(),
                SceneName = ActiveSceneName,
                Timestamp = timestamp,
                SelectedRulePackPath = settings.defaultRulePackPath,
                OptionalDeltaPatchPath = OptionalDeltaPatchPath,
                AllowModifyExistingAssets = false
            };

            _lastResult = module.Execute(ctx);
            USD_SnapshotUtil.SaveSnapshot(ActiveSceneName, timestamp, _lastResult.Snapshot);
            WriteReport(_lastResult, timestamp);
        }

        private void WriteReport(USD_ModuleResult result, string timestamp)
        {
            var report = new USD_Report
            {
                module = result.ModuleName,
                sceneName = ActiveSceneName,
                timestamp = timestamp,
                snapshot = result.Snapshot,
                workOrders = new List<USD_WorkOrder>(result.WorkOrders),
                appliedChanges = new List<string>(result.AppliedChanges),
                warnings = new List<string>(result.Warnings)
            };
            USD_ReportUtil.WriteReport(ActiveSceneName, timestamp, report);
        }

        private void ExportLastReport()
        {
            if (_lastResult == null) return;
            WriteReport(_lastResult, USD_EditorUtil.Timestamp);
            ShowNotification(new GUIContent("Report exported"));
        }

        private static void DrawReports()
        {
            EditorGUILayout.HelpBox("Reports are generated under Assets/_Tools/URPSceneDoctor/Reports/{SceneName}", MessageType.Info);
        }

        private static void DrawSettings()
        {
            var settings = USD_Settings.GetOrCreateSettings();
            var so = new SerializedObject(settings);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("reportsRoot"));
            EditorGUILayout.PropertyField(so.FindProperty("snapshotsRoot"));
            EditorGUILayout.PropertyField(so.FindProperty("patchesRoot"));
            EditorGUILayout.PropertyField(so.FindProperty("defaultRulePackPath"));
            EditorGUILayout.PropertyField(so.FindProperty("verboseLogs"));
            EditorGUILayout.PropertyField(so.FindProperty("defaultApplyStrength"));
            so.ApplyModifiedProperties();
        }
    }
}
