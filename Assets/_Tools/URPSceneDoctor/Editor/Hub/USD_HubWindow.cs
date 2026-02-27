using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace URPSceneDoctor.Editor
{
    public sealed class USD_HubWindow : EditorWindow
    {
        public const string ToolVersion = "v0.2";

        private readonly string[] _tabs = { "Atmosphere Doctor", "Render Doctor", "Tuning (Before/After)", "Reports", "Settings" };
        private int _selectedTab;
        private USD_AtmosAuditModule _atmosModule;
        private USD_RenderAuditModule _renderModule;
        private USD_TuningModule _tuningModule;
        private USD_ModuleResult _lastResult;
        private bool _assignNewProfileToExistingGlobalVolume;

        public string OptionalDeltaPatchPath;
        public string LastReportPath { get; private set; }

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
            EditorGUILayout.HelpBox($"URP Scene Doctor {ToolVersion} | Scene: {ActiveSceneName} | URP: {snapshot.activeURPAssetName} | Renderer: {snapshot.activeRendererDataName} | Global Volume: {snapshot.hasGlobalVolume}", MessageType.None);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Demo Scene", GUILayout.Width(150)))
            {
                USD_DemoSceneUtil.OpenOrCreateDemoSceneWithPrompt();
            }

            if (GUILayout.Button("Quick Verify", GUILayout.Width(150)))
            {
                RunQuickVerify();
            }
            EditorGUILayout.EndHorizontal();
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
            if (GUILayout.Button("Scan")) RunModule(module, USD_RunMode.Scan, true);
            if (GUILayout.Button("Dry Run")) RunModule(module, USD_RunMode.DryRun, true);
            if (GUILayout.Button("Apply")) RunModule(module, USD_RunMode.Apply, true);
            if (GUILayout.Button("Export Report") && _lastResult != null) ExportLastReport();
            EditorGUILayout.EndHorizontal();

            if (module.ModuleName == "Atmosphere Doctor")
            {
                _assignNewProfileToExistingGlobalVolume = EditorGUILayout.ToggleLeft(
                    "Assign new profile to existing Global Volume (default OFF)",
                    _assignNewProfileToExistingGlobalVolume);
            }
        }

        private void RunModule(IToolModule module, USD_RunMode mode, bool saveSnapshot)
        {
            var timestamp = USD_EditorUtil.Timestamp;
            var settings = USD_Settings.GetOrCreateSettings();
            var sceneName = string.IsNullOrWhiteSpace(ActiveSceneName) ? "UntitledScene" : ActiveSceneName;
            var ctx = new USD_RunContext
            {
                Mode = mode,
                Snapshot = USD_AtmosScanner.CaptureSnapshot(),
                SceneName = sceneName,
                Timestamp = timestamp,
                SelectedRulePackPath = settings.defaultRulePackPath,
                OptionalDeltaPatchPath = OptionalDeltaPatchPath,
                AllowModifyExistingAssets = _assignNewProfileToExistingGlobalVolume
            };

            _lastResult = module.Execute(ctx);
            if (saveSnapshot)
            {
                USD_SnapshotUtil.SaveSnapshot(sceneName, timestamp, _lastResult.Snapshot);
            }

            LastReportPath = WriteReport(_lastResult, timestamp, sceneName);
        }

        private string WriteReport(USD_ModuleResult result, string timestamp, string sceneName)
        {
            var report = new USD_Report
            {
                toolVersion = ToolVersion,
                module = result.ModuleName,
                sceneName = sceneName,
                timestamp = timestamp,
                snapshot = result.Snapshot,
                workOrders = new List<USD_WorkOrder>(result.WorkOrders),
                appliedChanges = new List<string>(result.AppliedChanges),
                warnings = new List<string>(result.Warnings)
            };

            if (!string.IsNullOrEmpty(OptionalDeltaPatchPath))
            {
                var patch = USD_SnapshotUtil.LoadDeltaPatch(OptionalDeltaPatchPath);
                if (patch != null)
                {
                    report.personalDeltaHints = new List<string>(patch.recommendedRanges);
                }
            }

            return USD_ReportUtil.WriteReport(sceneName, timestamp, report);
        }

        private void ExportLastReport()
        {
            if (_lastResult == null) return;
            var sceneName = string.IsNullOrWhiteSpace(ActiveSceneName) ? "UntitledScene" : ActiveSceneName;
            LastReportPath = WriteReport(_lastResult, USD_EditorUtil.Timestamp, sceneName);
            ShowNotification(new GUIContent("Report exported"));
        }

        private void RunQuickVerify()
        {
            var sceneName = string.IsNullOrWhiteSpace(ActiveSceneName) ? "UntitledScene" : ActiveSceneName;
            RunModule(_atmosModule, USD_RunMode.Scan, true);
            var hitCount = _lastResult != null ? _lastResult.WorkOrders.Count : 0;
            var warningCount = _lastResult != null ? _lastResult.Warnings.Count : 0;
            EditorUtility.DisplayDialog(
                "Quick Verify Complete",
                $"Report Path: {LastReportPath}\nMatched Rules: {hitCount}\nWarnings: {warningCount}",
                "OK");
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
