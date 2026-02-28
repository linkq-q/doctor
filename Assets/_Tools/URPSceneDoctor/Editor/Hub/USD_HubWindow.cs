using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace URPSceneDoctor.Editor
{
    public sealed class USD_HubWindow : EditorWindow
    {
        public const string ToolVersion = "v0.64";

        private string[] _tabs;
        private int _selectedTab;
        private USD_AtmosAuditModule _atmosModule;
        private USD_RenderAuditModule _renderModule;
        private USD_TuningModule _tuningModule;
        private USD_EvidencePackModule _evidenceModule;
        private USD_DeltaLibraryModule _deltaLibraryModule;
        private USD_PipelinePackModule _pipelinePackModule;
        private USD_BatchSamplerModule _batchSamplerModule;
        private USD_AiAssistModule _aiAssistModule;
        private USD_AiTuningModule _aiTuningModule;
        private USD_MetricsPanelModule _metricsPanelModule;
        private Vector2 _rightScroll;
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

        public USD_BrightnessBucket PolicyBrightnessBucket
        {
            get
            {
                var b = USD_Settings.GetOrCreateSettings().policyBrightnessBucket;
                return b == "High" ? USD_BrightnessBucket.High : (b == "Low" ? USD_BrightnessBucket.Low : USD_BrightnessBucket.Mid);
            }
        }

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
            _pipelinePackModule = new USD_PipelinePackModule();
            _batchSamplerModule = new USD_BatchSamplerModule();
            _aiAssistModule = new USD_AiAssistModule();
            _aiTuningModule = new USD_AiTuningModule();
            _metricsPanelModule = new USD_MetricsPanelModule();

            var settings = USD_Settings.GetOrCreateSettings();
            _activeTastePolicy = settings.defaultTastePolicy != null ? settings.defaultTastePolicy : USD_TastePolicyUtil.GetOrCreateDefaultPolicy();
            _screenshotWidth = settings.screenshotWidth;
            _screenshotHeight = settings.screenshotHeight;
            _learningEnabled = settings.enableLearningHints;
            _styleProfiles = USD_StyleProfileUtil.GetOrCreateBuiltIns();
            _selectedStyleIndex = 0;
            RefreshTabs();
            RefreshHeaderSnapshot(true);
        }

        private void RefreshTabs()
        {
            _tabs = new[]
            {
                USD_Localization.T("tab.atmos"), USD_Localization.T("tab.render"), USD_Localization.T("tab.tuning"), USD_Localization.T("tab.evidence"),
                USD_Localization.T("tab.delta"), USD_Localization.T("tab.pipeline"), USD_Localization.T("tab.batch"), USD_Localization.T("tab.ai"), USD_Localization.T("tab.aituning"), USD_Localization.T("tab.metrics"), USD_Localization.T("tab.reports"), USD_Localization.T("tab.settings")
            };
        }

        private void OnFocus()
        {
            RefreshHeaderSnapshot(true);
            RefreshTabs();
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
            if (GUILayout.Button(USD_Loc.C("hub.refreshHeader"), GUILayout.Width(130))) RefreshHeaderSnapshot(true);
            if (GUILayout.Button(USD_Loc.C("hub.openDemo"), GUILayout.Width(150)))
            {
                USD_DemoSceneUtil.OpenOrCreateDemoSceneWithPrompt();
                RefreshHeaderSnapshot(true);
            }

            if (GUILayout.Button(USD_Loc.C("hub.quickVerify"), GUILayout.Width(150))) RunQuickVerify();
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
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll, GUILayout.ExpandHeight(true));
            switch (_selectedTab)
            {
                case 0: _atmosModule.DrawUI(this); break;
                case 1: _renderModule.DrawUI(this); break;
                case 2: _tuningModule.DrawUI(this); break;
                case 3: _evidenceModule.DrawUI(this); break;
                case 4: _deltaLibraryModule.DrawUI(this); break;
                case 5: _pipelinePackModule.DrawUI(this); break;
                case 6: _batchSamplerModule.DrawUI(this); break;
                case 7: _aiAssistModule.DrawUI(this); break;
                case 8: _aiTuningModule.DrawUI(this); break;
                case 9: _metricsPanelModule.DrawUI(this); break;
                case 10: DrawReports(); break;
                case 11: DrawSettings(); break;
            }

            if (_lastResult != null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField(USD_Loc.T("hub.lastWorkOrders"), EditorStyles.boldLabel);
                foreach (var wo in _lastResult.WorkOrders)
                {
                    EditorGUILayout.LabelField($"[{wo.id}] {wo.title} ({wo.severity}) score={wo.sortScore:0.00}");
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        public void DrawExecutionButtons(IToolModule module)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(USD_Localization.T("btn.scan"))) RunModule(module, USD_RunMode.Scan, true);
            if (GUILayout.Button(USD_Localization.T("btn.dryrun"))) RunModule(module, USD_RunMode.DryRun, true);
            if (GUILayout.Button(USD_Localization.T("btn.apply"))) RunModule(module, USD_RunMode.Apply, true);
            if (GUILayout.Button(USD_Localization.T("btn.export")) && _lastResult != null) ExportLastReport();
            EditorGUILayout.EndHorizontal();

            if (module.ModuleName == "Atmosphere Doctor") DrawApplyOptions();
        }

        private void DrawApplyOptions()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(USD_Loc.T("hub.applyOptions"), EditorStyles.boldLabel);
            _applyMode = (USD_ApplyMode)EditorGUILayout.EnumPopup(USD_Loc.C("hub.applyMode"), _applyMode);

            var names = new string[_styleProfiles.Length];
            for (var i = 0; i < _styleProfiles.Length; i++)
            {
                names[i] = _styleProfiles[i] != null ? _styleProfiles[i].profileName : "(missing)";
            }

            _selectedStyleIndex = _styleProfiles.Length == 0 ? 0 : Mathf.Clamp(EditorGUILayout.Popup(USD_Loc.T("hub.styleProfile"), _selectedStyleIndex, names), 0, _styleProfiles.Length - 1);
            _assignNewProfileToExistingGlobalVolume = EditorGUILayout.ToggleLeft(USD_Loc.T("hub.bindPolicy"), _assignNewProfileToExistingGlobalVolume);
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

            var policyCheck = USD_PolicyChecker.Evaluate(report.snapshot, _activeTastePolicy, PolicyBrightnessBucket);
            report.policyPassCount = policyCheck.passed;
            report.policyWarningCount = policyCheck.warnings;
            report.policyChecklist = policyCheck.items;
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
            ShowNotification(new GUIContent(USD_Loc.T("hub.reportExported")));
        }

        private void RunQuickVerify()
        {
            RunModule(_atmosModule, USD_RunMode.Scan, true);
            var hitCount = _lastResult != null ? _lastResult.WorkOrders.Count : 0;
            var warningCount = _lastResult != null ? _lastResult.Warnings.Count : 0;
            EditorUtility.DisplayDialog(USD_Loc.T("quickverify.title"), $"{USD_Loc.T("quickverify.reportPath")}: {LastReportPath}\n{USD_Loc.T("quickverify.matched")}: {hitCount}\n{USD_Loc.T("quickverify.warnings")}: {warningCount}", USD_Loc.T("common.ok"));
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
            EditorGUILayout.HelpBox(USD_Loc.T("reports.hint"), MessageType.Info);
        }

        private void DrawSettings()
        {
            var settings = USD_Settings.GetOrCreateSettings();
            EditorGUILayout.HelpBox(USD_Loc.T("help.settings.overview"), MessageType.Info);
            var so = new SerializedObject(settings);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("reportsRoot"));
            EditorGUILayout.PropertyField(so.FindProperty("snapshotsRoot"));
            EditorGUILayout.PropertyField(so.FindProperty("patchesRoot"));
            EditorGUILayout.PropertyField(so.FindProperty("defaultRulePackPath"));
            EditorGUILayout.PropertyField(so.FindProperty("batchSceneRoot"));
            EditorGUILayout.PropertyField(so.FindProperty("verboseLogs"));
            EditorGUILayout.PropertyField(so.FindProperty("defaultApplyStrength"));
            EditorGUILayout.PropertyField(so.FindProperty("screenshotWidth"));
            EditorGUILayout.PropertyField(so.FindProperty("screenshotHeight"));
            EditorGUILayout.PropertyField(so.FindProperty("cameraMode"));
            EditorGUILayout.PropertyField(so.FindProperty("manualCamera"));
            var langOptions = new[] { "Auto", "中文", "English" };
            var currentMode = USD_Loc.GetLanguageMode();
            var modeIdx = Mathf.Max(0, System.Array.IndexOf(langOptions, currentMode));
            var newIdx = EditorGUILayout.Popup(USD_Loc.T("settings.language"), modeIdx, new[] { USD_Loc.T("lang.auto"), USD_Loc.T("lang.zh"), USD_Loc.T("lang.en") });
            if (newIdx != modeIdx)
            {
                USD_Loc.SetLanguageMode(langOptions[newIdx]);
                settings.language = langOptions[newIdx];
            }

            var promptLang = so.FindProperty("promptLanguage");
            var promptIdx = promptLang.stringValue == "en" ? 1 : 0;
            var promptNewIdx = EditorGUILayout.Popup(USD_Loc.T("settings.promptLang"), promptIdx, new[] { "中文", "English" });
            promptLang.stringValue = promptNewIdx == 1 ? "en" : "zh";
            EditorGUILayout.PropertyField(so.FindProperty("enableLearningHints"));
            EditorGUILayout.PropertyField(so.FindProperty("policyBrightnessBucket"));
            EditorGUILayout.PropertyField(so.FindProperty("labelCatalog"));
            EditorGUILayout.PropertyField(so.FindProperty("defaultTastePolicy"));
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(USD_Loc.T("settings.llmProvider"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(USD_Loc.T("help.settings.llm"), MessageType.Info);
            EditorGUILayout.PropertyField(so.FindProperty("llmProvider"), USD_Loc.C("settings.llmProvider"));
            EditorGUILayout.PropertyField(so.FindProperty("llmBaseUrl"), USD_Loc.C("settings.llmBaseUrl"));
            EditorGUILayout.PropertyField(so.FindProperty("llmModel"), USD_Loc.C("settings.llmModel"));
            EditorGUILayout.PropertyField(so.FindProperty("llmTimeoutSec"), USD_Loc.C("settings.llmTimeout"));
            EditorGUILayout.PropertyField(so.FindProperty("llmMaxTokens"), USD_Loc.C("settings.llmMaxTokens"));
            EditorGUILayout.PropertyField(so.FindProperty("llmTemperature"), USD_Loc.C("settings.llmTemp"));
            EditorGUILayout.PropertyField(so.FindProperty("smhMaxOffset"), USD_Loc.C("settings.smhMaxOffset"));
            var apiKey = USD_LlmClient.GetApiKey();
            var newApiKey = EditorGUILayout.PasswordField(USD_Loc.T("settings.apiKey"), apiKey);
            if (newApiKey != apiKey) USD_LlmClient.SetApiKey(newApiKey);
            so.ApplyModifiedProperties();

            if (settings.labelCatalog == null) settings.labelCatalog = USD_LabelCatalogUtil.GetOrCreateDefault();
            if (settings.labelCatalog != null && USD_LabelCatalogUtil.HasDuplicateIds(settings.labelCatalog, out var dup))
            {
                EditorGUILayout.HelpBox(USD_Loc.T("settings.dupLabelId", dup), MessageType.Warning);
            }

            _activeTastePolicy = settings.defaultTastePolicy != null ? settings.defaultTastePolicy : _activeTastePolicy;
            _screenshotWidth = settings.screenshotWidth;
            _screenshotHeight = settings.screenshotHeight;
            _learningEnabled = settings.enableLearningHints;
            RefreshTabs();
        }
    }
}
