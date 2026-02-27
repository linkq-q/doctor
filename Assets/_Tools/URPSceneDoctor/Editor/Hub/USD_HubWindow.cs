using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace URPSceneDoctor.Editor
{
    public sealed class USD_HubWindow : EditorWindow
    {
        public const string ToolVersion = "v0.3";

        private readonly string[] _tabs = { "Atmosphere Doctor", "Render Doctor", "Tuning (Before/After)", "Evidence Pack", "Delta Library", "Reports", "Settings" };
        private int _selectedTab;
        private USD_AtmosAuditModule _atmosModule;
        private USD_RenderAuditModule _renderModule;
        private USD_TuningModule _tuningModule;
        private USD_EvidencePackModule _evidenceModule;
        private USD_DeltaLibraryModule _deltaLibraryModule;
        private USD_ModuleResult _lastResult;
        private bool _assignNewProfileToExistingGlobalVolume;

        public string OptionalDeltaPatchPath;
        public string LastReportPath { get; private set; }
        public string LastBeforeSnapshotPath => _tuningModule != null ? _tuningModule.BeforeSnapshotPath : string.Empty;
        public string LastAfterSnapshotPath => _tuningModule != null ? _tuningModule.AfterSnapshotPath : string.Empty;
        public USD_TastePolicyAsset ActiveTastePolicy => _activeTastePolicy;
        public int ScreenshotWidth => _screenshotWidth;
        public int ScreenshotHeight => _screenshotHeight;
        public USD_DeltaStats CurrentLearningStats => _learningStats;

        private USD_TastePolicyAsset _activeTastePolicy;
        private USD_DeltaStats _learningStats;
        private int _screenshotWidth = 1920;
        private int _screenshotHeight = 1080;
        private bool _learningEnabled = true;

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
            _evidenceModule = new USD_EvidencePackModule();
            _deltaLibraryModule = new USD_DeltaLibraryModule();
            var settings = USD_Settings.GetOrCreateSettings();
            _activeTastePolicy = settings.defaultTastePolicy != null ? settings.defaultTastePolicy : USD_TastePolicyUtil.GetOrCreateDefaultPolicy();
            _screenshotWidth = settings.screenshotWidth;
            _screenshotHeight = settings.screenshotHeight;
            _learningEnabled = settings.enableLearningHints;
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
                    _evidenceModule.DrawUI(this);
                    break;
                case 4:
                    _deltaLibraryModule.DrawUI(this);
                    break;
                case 5:
                    DrawReports();
                    break;
                case 6:
                    DrawSettings();
                    break;
            }

            if (_lastResult != null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Last Work Orders", EditorStyles.boldLabel);
                foreach (var wo in _lastResult.WorkOrders)
                {
                    EditorGUILayout.LabelField($"[{wo.id}] {wo.title} ({wo.severity}) score={wo.sortScore:0.00}");
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
                AllowModifyExistingAssets = _assignNewProfileToExistingGlobalVolume,
                TastePolicy = _activeTastePolicy,
                LearningStats = _learningEnabled ? _learningStats : null
            };

            _lastResult = module.Execute(ctx);
            if (saveSnapshot)
            {
                USD_SnapshotUtil.SaveSnapshot(sceneName, timestamp, _lastResult.Snapshot);
            }

            LastReportPath = WriteReport(_lastResult, timestamp, sceneName);
        }

        public USD_ModuleResult RunAtmosScanForExternal(string timestamp)
        {
            var settings = USD_Settings.GetOrCreateSettings();
            var sceneName = string.IsNullOrWhiteSpace(ActiveSceneName) ? "UntitledScene" : ActiveSceneName;
            var ctx = new USD_RunContext
            {
                Mode = USD_RunMode.Scan,
                Snapshot = USD_AtmosScanner.CaptureSnapshot(),
                SceneName = sceneName,
                Timestamp = timestamp,
                SelectedRulePackPath = settings.defaultRulePackPath,
                OptionalDeltaPatchPath = OptionalDeltaPatchPath,
                TastePolicy = _activeTastePolicy,
                LearningStats = _learningEnabled ? _learningStats : null
            };
            _lastResult = _atmosModule.Execute(ctx);
            LastReportPath = WriteReport(_lastResult, timestamp, sceneName);
            return _lastResult;
        }

        public USD_Report BuildReportFromResult(USD_ModuleResult result, string sceneName, string timestamp)
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
                warnings = new List<string>(result.Warnings),
                tastePolicyName = _activeTastePolicy != null ? _activeTastePolicy.policyName : "(none)",
                learningSummary = _learningEnabled && _learningStats != null ? $"Learning enabled: {_learningStats.sampleCount} samples; Top hint: {_learningStats.topHints[0]}" : "Learning disabled"
            };

            if (_activeTastePolicy != null)
            {
                report.tastePriorityOrder = new List<string>(_activeTastePolicy.priorityOrder);
                report.tasteForbiddenActions = new List<string>(_activeTastePolicy.forbiddenActions);
            }

            if (!string.IsNullOrEmpty(OptionalDeltaPatchPath))
            {
                var patch = USD_SnapshotUtil.LoadDeltaPatch(OptionalDeltaPatchPath);
                if (patch != null)
                {
                    report.personalDeltaHints = new List<string>(patch.recommendedRanges);
                }
            }
            else if (_learningStats != null)
            {
                report.personalDeltaHints = new List<string>(_learningStats.topHints);
            }

            return report;
        }

        private string WriteReport(USD_ModuleResult result, string timestamp, string sceneName)
        {
            var report = BuildReportFromResult(result, sceneName, timestamp);
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
            RunModule(_atmosModule, USD_RunMode.Scan, true);
            var hitCount = _lastResult != null ? _lastResult.WorkOrders.Count : 0;
            var warningCount = _lastResult != null ? _lastResult.Warnings.Count : 0;
            EditorUtility.DisplayDialog(
                "Quick Verify Complete",
                $"Report Path: {LastReportPath}\nMatched Rules: {hitCount}\nWarnings: {warningCount}",
                "OK");
        }

        public void SetLearningStats(USD_DeltaStats stats)
        {
            _learningStats = stats;
        }

        public void CreateEvidencePack()
        {
            _evidenceModule.CreatePack(this);
        }

        private static void DrawReports()
        {
            EditorGUILayout.HelpBox("Reports are generated under Assets/_Tools/URPSceneDoctor/Reports/{SceneName}", MessageType.Info);
        }

        private void DrawSettings()
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
            EditorGUILayout.PropertyField(so.FindProperty("screenshotWidth"));
            EditorGUILayout.PropertyField(so.FindProperty("screenshotHeight"));
            EditorGUILayout.PropertyField(so.FindProperty("enableLearningHints"));
            EditorGUILayout.PropertyField(so.FindProperty("defaultTastePolicy"));
            so.ApplyModifiedProperties();

            _activeTastePolicy = settings.defaultTastePolicy != null ? settings.defaultTastePolicy : _activeTastePolicy;
            _screenshotWidth = settings.screenshotWidth;
            _screenshotHeight = settings.screenshotHeight;
            _learningEnabled = settings.enableLearningHints;
        }
    }
}
