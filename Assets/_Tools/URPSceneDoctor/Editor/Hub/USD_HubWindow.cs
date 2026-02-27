using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace URPSceneDoctor.Editor
{
    public sealed class USD_HubWindow : EditorWindow
    {
        public const string ToolVersion = "v0.4";

        private readonly string[] _tabs = { "Atmosphere Doctor", "Render Doctor", "Tuning (Before/After)", "Evidence Pack", "Delta Library", "Reports", "Settings" };
        private int _selectedTab;
        private USD_AtmosAuditModule _atmosModule;
        private USD_RenderAuditModule _renderModule;
        private USD_TuningModule _tuningModule;
        private USD_EvidencePackModule _evidenceModule;
        private USD_DeltaLibraryModule _deltaLibraryModule;
        private USD_ModuleResult _lastResult;
        private bool _assignNewProfileToExistingGlobalVolume;
        private USD_ApplyMode _applyMode = USD_ApplyMode.SafeNeutral;
        private USD_StyleProfileAsset[] _styleProfiles = new USD_StyleProfileAsset[0];
        private int _selectedStyleIndex;

        private USD_ScanSnapshot _headerSnapshot;
        private double _nextHeaderRefreshTime;
        private const double HeaderRefreshIntervalSec = 1.0;

        public string OptionalDeltaPatchPath;
        public string LastReportPath { get; private set; }
        public string LastBeforeSnapshotPath => _tuningModule != null ? _tuningModule.BeforeSnapshotPath : string.Empty;
        public string LastAfterSnapshotPath => _tuningModule != null ? _tuningModule.AfterSnapshotPath : string.Empty;
        public USD_TastePolicyAsset ActiveTastePolicy => _activeTastePolicy;
        public int ScreenshotWidth => _screenshotWidth;
        public int ScreenshotHeight => _screenshotHeight;
        public USD_DeltaStats CurrentLearningStats => _learningStats;
        public USD_ApplyMode ApplyMode => _applyMode;
        public USD_StyleProfileAsset SelectedStyleProfile => (_styleProfiles != null && _styleProfiles.Length > 0 && _selectedStyleIndex >= 0 && _selectedStyleIndex < _styleProfiles.Length) ? _styleProfiles[_selectedStyleIndex] : null;
        public bool AssignNewProfileToExistingGlobalVolume => _assignNewProfileToExistingGlobalVolume;

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
            _styleProfiles = USD_StyleProfileUtil.GetOrCreateBuiltIns();
            _selectedStyleIndex = 0;
            RefreshHeaderSnapshot(true);
        }

        private void OnFocus()
        {
            RefreshHeaderSnapshot(true);
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
            if (_headerSnapshot == null || EditorApplication.timeSinceStartup >= _nextHeaderRefreshTime)
            {
                RefreshHeaderSnapshot(false);
            }

            var snapshot = _headerSnapshot ?? new USD_ScanSnapshot();
            EditorGUILayout.HelpBox($"URP Scene Doctor {ToolVersion} | Scene: {ActiveSceneName} | URP: {snapshot.activeURPAssetName} | Renderer: {snapshot.activeRendererDataName} | Global Volume: {snapshot.hasGlobalVolume}", MessageType.None);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Header", GUILayout.Width(130)))
            {
                RefreshHeaderSnapshot(true);
            }

            if (GUILayout.Button("Open Demo Scene", GUILayout.Width(150)))
            {
                USD_DemoSceneUtil.OpenOrCreateDemoSceneWithPrompt();
                RefreshHeaderSnapshot(true);
            }

            if (GUILayout.Button("Quick Verify", GUILayout.Width(150)))
            {
                RunQuickVerify();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void RefreshHeaderSnapshot(bool force)
        {
            if (!force && EditorApplication.timeSinceStartup < _nextHeaderRefreshTime && _headerSnapshot != null) return;
            _headerSnapshot = USD_AtmosScanner.CaptureSnapshot();
            _nextHeaderRefreshTime = EditorApplication.timeSinceStartup + HeaderRefreshIntervalSec;
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
                DrawApplyOptions();
            }
        }

        private void DrawApplyOptions()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Apply Options", EditorStyles.boldLabel);
            _applyMode = (USD_ApplyMode)EditorGUILayout.EnumPopup("Apply Mode", _applyMode);

            var names = new string[_styleProfiles.Length];
            for (var i = 0; i < _styleProfiles.Length; i++)
            {
                names[i] = _styleProfiles[i] != null ? _styleProfiles[i].profileName : "(missing)";
            }

            _selectedStyleIndex = _styleProfiles.Length == 0 ? 0 : Mathf.Clamp(EditorGUILayout.Popup("Style Profile", _selectedStyleIndex, names), 0, _styleProfiles.Length - 1);
            _assignNewProfileToExistingGlobalVolume = EditorGUILayout.ToggleLeft(
                "Assign new profile to existing Global Volume (default OFF)",
                _assignNewProfileToExistingGlobalVolume);
        }

        private void RunModule(IToolModule module, USD_RunMode mode, bool saveSnapshot)
        {
            var ctx = BuildRunContext(mode, USD_EditorUtil.Timestamp);
            _lastResult = module.Execute(ctx);
            if (saveSnapshot)
            {
                USD_SnapshotUtil.SaveSnapshot(ctx.SceneName, ctx.Timestamp, _lastResult.Snapshot);
            }

            LastReportPath = WriteReport(_lastResult, ctx.Timestamp, ctx.SceneName);
            RefreshHeaderSnapshot(true);
        }

        private USD_RunContext BuildRunContext(USD_RunMode mode, string timestamp)
        {
            var settings = USD_Settings.GetOrCreateSettings();
            var sceneName = string.IsNullOrWhiteSpace(ActiveSceneName) ? "UntitledScene" : ActiveSceneName;
            return new USD_RunContext
            {
                Mode = mode,
                Snapshot = USD_AtmosScanner.CaptureSnapshot(),
                SceneName = sceneName,
                Timestamp = timestamp,
                SelectedRulePackPath = settings.defaultRulePackPath,
                OptionalDeltaPatchPath = OptionalDeltaPatchPath,
                AllowModifyExistingAssets = _assignNewProfileToExistingGlobalVolume,
                TastePolicy = _activeTastePolicy,
                LearningStats = _learningEnabled ? _learningStats : null,
                ApplyMode = _applyMode,
                StyleProfile = SelectedStyleProfile
            };
        }

        public USD_ModuleResult RunAtmosForExternal(string timestamp, USD_RunMode mode)
        {
            var ctx = BuildRunContext(mode, timestamp);
            _lastResult = _atmosModule.Execute(ctx);
            LastReportPath = WriteReport(_lastResult, timestamp, ctx.SceneName);
            RefreshHeaderSnapshot(true);
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
                learningSummary = _learningEnabled && _learningStats != null && _learningStats.topHints.Count > 0
                    ? $"Learning enabled: {_learningStats.sampleCount} samples; Top hint: {_learningStats.topHints[0]}"
                    : "Learning disabled",
                applyMode = _applyMode.ToString(),
                styleProfileName = SelectedStyleProfile != null ? SelectedStyleProfile.profileName : "Neutral Baseline",
                bindPolicy = _assignNewProfileToExistingGlobalVolume ? "ON" : "OFF"
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
