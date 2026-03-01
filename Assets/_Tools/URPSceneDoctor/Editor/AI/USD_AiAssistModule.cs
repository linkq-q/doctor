using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    [Serializable]
    public sealed class USD_PairwisePreference
    {
        public string scene;
        public string styleA;
        public string styleB;
        public string choice;
        public List<string> reasons = new List<string>();
        public string timestamp;
    }

    [Serializable]
    public sealed class USD_Annotation
    {
        public string style_goal_id;
        public int score_1to10 = 5;
        public List<string> issue_tags = new List<string>();
        public List<string> next_steps = new List<string>();
        public string user_note;
        public string source = "manual";
        public string timestamp;
    }

    [Serializable]
    internal sealed class USD_AiDraftFile
    {
        public string timestamp;
        public string provider;
        public string model;
        public string raw_text;
        public string raw_json;
        public string error;
        public string status;
        public USD_AiLabelDraft parsed;
    }

    public sealed class USD_AiAssistModule : IToolModule
    {
        public string ModuleName => "AI Assist";

        private string _sampleFolder;
        private int _pairChoice;
        private string _styleA = "Clean Stylized";
        private string _styleB = "Warm Dusk";
        private readonly List<string> _selectedReasonIds = new List<string>();

        private bool _busy;
        private MessageType _statusType = MessageType.Info;
        private string _statusMessage = string.Empty;
        private string _lastOutputPath = string.Empty;

        private USD_AiLabelDraft _draft;
        private USD_Annotation _final = new USD_Annotation();
        private bool _draftLoaded;
        private bool _finalLoaded;
        private Vector2 _draftScroll;
        private readonly USD_LlmCallStatus _llmStatus = new USD_LlmCallStatus();

        private const string DraftFileName = "ai_label_draft.json";
        private const string AnnotationFileName = "annotation.json";
        private const string ActionLogFileName = "ai_action_log.jsonl";

        public void DrawUI(USD_HubWindow hub)
        {
            var settings = USD_Settings.GetOrCreateSettings();
            var catalog = settings.labelCatalog != null ? settings.labelCatalog : USD_LabelCatalogUtil.GetOrCreateDefault();

            if (catalog == null)
            {
                EditorGUILayout.HelpBox(USD_Loc.T("ai.errNoCatalog"), MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox(USD_Loc.T("help.ai.overview.v062"), MessageType.Info);
            EditorGUILayout.HelpBox(USD_LlmClient.IsEnabled(settings) ? USD_Loc.T("ai.enabled") : USD_Loc.T("ai.disabled"), USD_LlmClient.IsEnabled(settings) ? MessageType.Info : MessageType.Warning);
            DrawLlmStatus();

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
                if (!string.IsNullOrEmpty(_lastOutputPath))
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(USD_Loc.C("ai.openFolder")))
                    {
                        var folder = Directory.Exists(_lastOutputPath) ? _lastOutputPath : Path.GetDirectoryName(_lastOutputPath);
                        if (!string.IsNullOrEmpty(folder)) EditorUtility.RevealInFinder(folder.Replace('\\', '/'));
                    }

                    if (GUILayout.Button(USD_Loc.C("ai.openFile")) && File.Exists(_lastOutputPath))
                    {
                        EditorUtility.RevealInFinder(_lastOutputPath.Replace('\\', '/'));
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            DrawFolderPanel();
            DrawDetectedFiles();

            if (_busy)
            {
                EditorGUILayout.HelpBox(USD_Loc.T("ai.busy"), MessageType.Info);
                return;
            }

            DrawDraftPanel(settings, catalog);
            DrawFinalPanel(catalog);
            DrawPairwisePanel(settings, catalog);
        }

        public USD_ModuleResult Execute(USD_RunContext context)
        {
            return new USD_ModuleResult { ModuleName = ModuleName, Snapshot = context.Snapshot ?? USD_AtmosScanner.CaptureSnapshot() };
        }


        public static USD_AiLabelDraft LoadParsedDraft(string draftPath)
        {
            if (!File.Exists(draftPath)) return null;
            try
            {
                var text = File.ReadAllText(draftPath);
                var wrapped = JsonUtility.FromJson<USD_AiDraftFile>(text);
                if (wrapped != null && wrapped.parsed != null) return wrapped.parsed;
                return JsonUtility.FromJson<USD_AiLabelDraft>(text);
            }
            catch
            {
                return null;
            }
        }

        public static USD_AiLabelDraft GenerateDraft(USD_Settings settings, string folder, string sceneName, USD_LabelCatalogAsset catalog, USD_ScanSnapshot before, USD_ScanSnapshot after, USD_DeltaPatch patch, USD_ImageMetricsFile metricsBefore, USD_ImageMetricsFile metricsAfter)
        {
            USD_AiLabelDraft parsed;
            string rawText = string.Empty;
            string rawJson = string.Empty;
            string error = string.Empty;

            if (USD_LlmClient.IsEnabled(settings))
            {
                var res = USD_LlmClient.Chat(settings,
                    USD_AiPromptTemplates.DraftLabelingSystem(settings.promptLanguage),
                    USD_AiPromptTemplates.DraftLabelingUser(catalog, sceneName, after, patch, metricsBefore, metricsAfter));
                rawText = res.text;
                rawJson = res.raw_json;
                if (res.success)
                {
                    parsed = USD_AiPromptTemplates.ParseLabelDraftOrFallback(res.text, catalog, metricsAfter);
                }
                else
                {
                    parsed = USD_AiPromptTemplates.ParseLabelDraftOrFallback(string.Empty, catalog, metricsAfter);
                    error = string.IsNullOrEmpty(res.error) ? "LLM failed" : res.error;
                }
            }
            else
            {
                parsed = USD_AiPromptTemplates.ParseLabelDraftOrFallback(string.Empty, catalog, metricsAfter);
                error = USD_Loc.T("ai.errLlmDisabled");
            }

            parsed.source = USD_LlmClient.IsEnabled(settings) ? "deepseek/openai-compatible" : "fallback-template";
            parsed.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            if (!string.IsNullOrEmpty(error)) parsed.error = error;

            WriteDraftFile(folder, settings, parsed, rawText, rawJson, string.IsNullOrEmpty(error) ? "generated" : "failed", error);
            return parsed;
        }

        private void DrawFolderPanel()
        {
            _sampleFolder = EditorGUILayout.TextField(USD_Loc.C("ai.sampleFolder"), _sampleFolder);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(USD_Loc.C("btn.pickFolder", "help.evidence.overview"), GUILayout.Width(140)))
            {
                Debug.Log("[USD][AI] Pick Folder clicked");
                var abs = EditorUtility.OpenFolderPanel(USD_Loc.T("btn.pickFolder"), Application.dataPath, string.Empty);
                if (!string.IsNullOrEmpty(abs))
                {
                    _sampleFolder = ToAssetPath(abs);
                    ReloadLocalState();
                }
            }

            if (GUILayout.Button(USD_Loc.C("btn.open"), GUILayout.Width(100)) && !string.IsNullOrEmpty(_sampleFolder))
            {
                EditorUtility.RevealInFinder(_sampleFolder);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(USD_Loc.T("ai.currentFolder"), string.IsNullOrEmpty(_sampleFolder) ? USD_Loc.T("common.none") : _sampleFolder);
        }

        private void DrawDetectedFiles()
        {
            if (string.IsNullOrWhiteSpace(_sampleFolder) || !Directory.Exists(_sampleFolder)) return;
            EditorGUILayout.LabelField(USD_Loc.T("ai.detectedFiles"), EditorStyles.boldLabel);
            var names = new[]
            {
                "snapshot_before.json", "snapshot_after.json", "deltaPatch.json", "diff.json", "report.json", "image_metrics_before.json", "image_metrics_after.json", DraftFileName, AnnotationFileName
            };
            foreach (var n in names)
            {
                var p = Path.Combine(_sampleFolder, n);
                EditorGUILayout.LabelField(File.Exists(p) ? "✓ " + n : "- " + n);
            }
        }

        private void DrawDraftPanel(USD_Settings settings, USD_LabelCatalogAsset catalog)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(USD_Loc.T("ai.draftPanel"), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(USD_Loc.C("ai.generate"), GUILayout.Width(120)))
            {
                GenerateCurrentDraft(settings, catalog, "generate");
            }
            if (GUILayout.Button(USD_Loc.C("ai.regenerate"), GUILayout.Width(120)))
            {
                GenerateCurrentDraft(settings, catalog, "regenerate");
            }
            if (GUILayout.Button(USD_Loc.C("ai.discard"), GUILayout.Width(120)))
            {
                RunSafeAction("discard", DiscardDraft);
            }
            EditorGUILayout.EndHorizontal();

            LoadDraftIfNeeded();
            if (_draft == null)
            {
                EditorGUILayout.HelpBox(USD_Loc.T("ai.draftNotGenerated"), MessageType.Warning);
                return;
            }

            DrawDraftForm(catalog);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(USD_Loc.C("batch.acceptAi"), GUILayout.Width(140)))
            {
                RunSafeAction("accept", AcceptDraft);
            }
            if (GUILayout.Button(USD_Loc.C("batch.rejectAi"), GUILayout.Width(140)))
            {
                RunSafeAction("reject", RejectDraft);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDraftForm(USD_LabelCatalogAsset catalog)
        {
            _draftScroll = EditorGUILayout.BeginScrollView(_draftScroll, GUILayout.MinHeight(200));
            _draft.recommended_style_goal_id = DrawStyleDropdown(catalog, _draft.recommended_style_goal_id, "ai.styleGoal", "help.ai.styleGoal");

            EditorGUILayout.BeginHorizontal();
            _draft.recommended_score_1to10 = EditorGUILayout.IntSlider(USD_Loc.C("ai.score", "help.ai.score"), _draft.recommended_score_1to10, 1, 10);
            _draft.recommended_score_1to10 = EditorGUILayout.IntField(_draft.recommended_score_1to10, GUILayout.Width(40));
            _draft.recommended_score_1to10 = Mathf.Clamp(_draft.recommended_score_1to10, 1, 10);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(USD_Loc.C("ai.issueTags", "help.ai.tags"));
            _draft.recommended_issue_tags_top3 = DrawIssueChecklist(catalog, _draft.recommended_issue_tags_top3, 6);

            EditorGUILayout.LabelField(USD_Loc.T("ai.nextSteps"));
            var nextText = string.Join("\n", _draft.next_steps ?? new List<string>());
            nextText = EditorGUILayout.TextArea(nextText, GUILayout.MinHeight(45));
            _draft.next_steps = nextText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).Take(3).ToList();

            EditorGUILayout.LabelField(USD_Loc.T("ai.shortReason"));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextArea(_draft.short_reason ?? string.Empty, GUILayout.MinHeight(40));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndScrollView();
        }

        private void DrawFinalPanel(USD_LabelCatalogAsset catalog)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(USD_Loc.T("ai.finalPanel"), EditorStyles.boldLabel);

            LoadFinalIfNeeded();
            if (_final == null) _final = new USD_Annotation();

            _final.style_goal_id = DrawStyleDropdown(catalog, _final.style_goal_id, "ai.styleGoal", "help.ai.styleGoal");

            EditorGUILayout.BeginHorizontal();
            _final.score_1to10 = EditorGUILayout.IntSlider(USD_Loc.C("ai.score", "help.ai.score"), _final.score_1to10, 1, 10);
            _final.score_1to10 = EditorGUILayout.IntField(_final.score_1to10, GUILayout.Width(40));
            _final.score_1to10 = Mathf.Clamp(_final.score_1to10, 1, 10);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(USD_Loc.C("ai.issueTags", "help.ai.tags"));
            _final.issue_tags = DrawIssueChecklist(catalog, _final.issue_tags, 6);

            EditorGUILayout.LabelField(USD_Loc.T("ai.nextSteps"));
            var nextText = string.Join("\n", _final.next_steps ?? new List<string>());
            nextText = EditorGUILayout.TextArea(nextText, GUILayout.MinHeight(45));
            _final.next_steps = nextText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).Take(3).ToList();

            _final.user_note = EditorGUILayout.TextField(USD_Loc.T("ai.userNote"), _final.user_note ?? string.Empty);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(USD_Loc.C("ai.saveAnnotation", "help.ai.saveAnnotation"), GUILayout.Width(160)))
            {
                RunSafeAction("save", () => SaveAnnotation("manual"));
            }
            if (GUILayout.Button(USD_Loc.C("ai.resetToDraft"), GUILayout.Width(140)))
            {
                RunSafeAction("reset_to_draft", ResetToDraft);
            }
            if (GUILayout.Button(USD_Loc.C("ai.clear"), GUILayout.Width(120)))
            {
                RunSafeAction("clear", ClearFinal);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPairwisePanel(USD_Settings settings, USD_LabelCatalogAsset catalog)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(USD_Loc.T("ai.pairwise"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(USD_Loc.T("help.pairwise"), MessageType.Info);

            _styleA = EditorGUILayout.TextField(USD_Loc.C("ai.styleA"), _styleA);
            _styleB = EditorGUILayout.TextField(USD_Loc.C("ai.styleB"), _styleB);
            _pairChoice = GUILayout.SelectionGrid(_pairChoice, new[] { USD_Loc.T("ai.aBetter"), USD_Loc.T("ai.bBetter"), USD_Loc.T("ai.tie") }, 3);

            foreach (var issue in catalog.issues)
            {
                var on = _selectedReasonIds.Contains(issue.id);
                var next = EditorGUILayout.ToggleLeft(USD_LabelCatalogUtil.DisplayIssue(issue), on);
                if (next && !on) _selectedReasonIds.Add(issue.id);
                if (!next && on) _selectedReasonIds.Remove(issue.id);
            }

            if (GUILayout.Button(USD_Loc.C("ai.savePairwise")))
            {
                RunSafeAction("pairwise", () => SavePairwise(_sampleFolder));
            }
        }

        private string DrawStyleDropdown(USD_LabelCatalogAsset catalog, string currentId, string labelKey, string tooltipKey)
        {
            var styles = catalog.styles ?? new List<USD_StyleGoal>();
            if (styles.Count == 0) return string.Empty;
            var names = styles.Select(USD_LabelCatalogUtil.DisplayStyle).ToArray();
            var idx = Mathf.Max(0, styles.FindIndex(x => x.id == currentId));
            idx = EditorGUILayout.Popup(USD_Loc.C(labelKey, tooltipKey), idx, names);
            return styles[idx].id;
        }

        private List<string> DrawIssueChecklist(USD_LabelCatalogAsset catalog, List<string> selected, int maxCount)
        {
            var next = new List<string>(selected ?? new List<string>());
            foreach (var issue in catalog.issues)
            {
                var on = next.Contains(issue.id);
                var enabledBefore = GUI.enabled;
                if (!on && next.Count >= maxCount) GUI.enabled = false;
                var newOn = EditorGUILayout.ToggleLeft(USD_LabelCatalogUtil.DisplayIssue(issue), on);
                GUI.enabled = enabledBefore;
                if (newOn && !on && next.Count < maxCount) next.Add(issue.id);
                if (!newOn && on) next.Remove(issue.id);
            }
            return next;
        }

        private void GenerateCurrentDraft(USD_Settings settings, USD_LabelCatalogAsset catalog, string actionName)
        {
            if (!ValidateFolder(_sampleFolder, out var msg)) { ShowError(msg); return; }
            if (_busy) return;

            var sceneName = Path.GetFileName(_sampleFolder);
            var before = JsonUtility.FromJson<USD_ScanSnapshot>(SafeRead(Path.Combine(_sampleFolder, "snapshot_before.json"))) ?? new USD_ScanSnapshot();
            var after = JsonUtility.FromJson<USD_ScanSnapshot>(SafeRead(Path.Combine(_sampleFolder, "snapshot_after.json"))) ?? new USD_ScanSnapshot();
            var patch = JsonUtility.FromJson<USD_DeltaPatch>(SafeRead(Path.Combine(_sampleFolder, "deltaPatch.json"))) ?? new USD_DeltaPatch();
            var mBefore = JsonUtility.FromJson<USD_ImageMetricsFile>(SafeRead(Path.Combine(_sampleFolder, "image_metrics_before.json"))) ?? new USD_ImageMetricsFile();
            var mAfter = JsonUtility.FromJson<USD_ImageMetricsFile>(SafeRead(Path.Combine(_sampleFolder, "image_metrics_after.json"))) ?? new USD_ImageMetricsFile();

            _busy = true;
            _llmStatus.MarkSending("已发送请求（Draft Labeling）...");

            if (!USD_LlmClient.IsEnabled(settings))
            {
                _draft = USD_AiPromptTemplates.ParseLabelDraftOrFallback(string.Empty, catalog, mAfter);
                _draft.source = "fallback-template";
                _draft.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                _draft.error = USD_Loc.T("ai.errLlmDisabled");
                WriteDraftFile(_sampleFolder, settings, _draft, string.Empty, string.Empty, "failed", _draft.error);
                _draftLoaded = true;
                _llmStatus.MarkFail(_draft.error);
                _statusType = MessageType.Warning;
                _statusMessage = USD_Loc.T("ai.generatedWarn", _draft.error, Path.Combine(_sampleFolder, DraftFileName));
                _lastOutputPath = Path.Combine(_sampleFolder, DraftFileName);
                AppendActionLog(_sampleFolder, actionName, "fail", _draft.error ?? string.Empty);
                _busy = false;
                AssetDatabase.Refresh();
                return;
            }

            var messages = new List<(string role, string content)>
            {
                ("system", USD_AiPromptTemplates.DraftLabelingSystem(settings.promptLanguage)),
                ("user", USD_AiPromptTemplates.DraftLabelingUser(catalog, sceneName, after, patch, mBefore, mAfter))
            };

            USD_LlmClient.ChatOnce(settings, messages,
                onOk: text =>
                {
                    _draft = USD_AiPromptTemplates.ParseLabelDraftOrFallback(text, catalog, mAfter);
                    _draft.source = "deepseek/openai-compatible";
                    _draft.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    WriteDraftFile(_sampleFolder, settings, _draft, text, string.Empty, "generated", string.Empty);
                    _draftLoaded = true;
                    _llmStatus.MarkSuccess("AI 回复成功：已生成草稿。");
                    _statusType = MessageType.Info;
                    _statusMessage = USD_Loc.T("ai.generatedOk", Path.Combine(_sampleFolder, DraftFileName));
                    _lastOutputPath = Path.Combine(_sampleFolder, DraftFileName);
                    AppendActionLog(_sampleFolder, actionName, "ok", string.Empty);
                    EditorUtility.DisplayDialog(USD_Loc.T("ai.dialogTitle"), USD_Loc.T("ai.generatedDialog"), USD_Loc.T("common.ok"));
                    _busy = false;
                    AssetDatabase.Refresh();
                },
                onFail: err =>
                {
                    _draft = USD_AiPromptTemplates.ParseLabelDraftOrFallback(string.Empty, catalog, mAfter);
                    _draft.source = "deepseek/openai-compatible";
                    _draft.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    _draft.error = err;
                    WriteDraftFile(_sampleFolder, settings, _draft, string.Empty, string.Empty, "failed", err);
                    _draftLoaded = true;
                    _llmStatus.MarkFail(err);
                    _statusType = MessageType.Warning;
                    _statusMessage = USD_Loc.T("ai.generatedWarn", err, Path.Combine(_sampleFolder, DraftFileName));
                    _lastOutputPath = Path.Combine(_sampleFolder, DraftFileName);
                    AppendActionLog(_sampleFolder, actionName, "fail", err ?? string.Empty);
                    _busy = false;
                    AssetDatabase.Refresh();
                });
        }

        private void AcceptDraft()
        {
            Debug.Log("[USD][AI] Accept clicked");
            if (_draft == null) throw new Exception(USD_Loc.T("ai.draftNotGenerated"));

            _final.style_goal_id = _draft.recommended_style_goal_id;
            _final.score_1to10 = _draft.recommended_score_1to10;
            _final.issue_tags = new List<string>(_draft.recommended_issue_tags_top3 ?? new List<string>());
            _final.next_steps = new List<string>(_draft.next_steps ?? new List<string>());
            _final.user_note = _final.user_note ?? string.Empty;
            SaveAnnotation("ai_accept");
            AppendActionLog(_sampleFolder, "accept", "ok", string.Empty);
            UpdateBatchSummary("accepted", _final);
        }

        private void RejectDraft()
        {
            Debug.Log("[USD][AI] Reject clicked");
            if (_draft == null) throw new Exception(USD_Loc.T("ai.draftNotGenerated"));
            AppendActionLog(_sampleFolder, "reject", "ok", string.Empty);
            UpdateDraftStatus("rejected", string.Empty);
            _statusType = MessageType.Warning;
            _statusMessage = USD_Loc.T("ai.rejected");
            _lastOutputPath = Path.Combine(_sampleFolder, DraftFileName);
            UpdateBatchSummary("rejected", _final);
        }

        private void DiscardDraft()
        {
            Debug.Log("[USD][AI] Discard clicked");
            if (!ValidateFolder(_sampleFolder, out var msg)) throw new Exception(msg);
            AppendActionLog(_sampleFolder, "discard", "ok", string.Empty);
            UpdateDraftStatus("discarded", string.Empty);
            _draft = null;
            _draftLoaded = true;
            _statusType = MessageType.Info;
            _statusMessage = USD_Loc.T("ai.discarded");
            _lastOutputPath = Path.Combine(_sampleFolder, DraftFileName);
        }

        private void SaveAnnotation(string source)
        {
            if (!ValidateFolder(_sampleFolder, out var msg)) throw new Exception(msg);
            _final.timestamp = DateTime.Now.ToString("s");
            _final.source = source;
            var path = Path.Combine(_sampleFolder, AnnotationFileName);
            File.WriteAllText(path, JsonUtility.ToJson(_final, true));
            AppendActionLog(_sampleFolder, "save", "ok", string.Empty);
            _statusType = MessageType.Info;
            _statusMessage = USD_Loc.T("ai.savedAnnotation", path);
            _lastOutputPath = path;
            _finalLoaded = true;
            UpdateBatchSummary(source == "manual" ? "edited" : "accepted", _final);
        }

        private void ResetToDraft()
        {
            if (_draft == null) throw new Exception(USD_Loc.T("ai.draftNotGenerated"));
            _final.style_goal_id = _draft.recommended_style_goal_id;
            _final.score_1to10 = _draft.recommended_score_1to10;
            _final.issue_tags = new List<string>(_draft.recommended_issue_tags_top3 ?? new List<string>());
            _final.next_steps = new List<string>(_draft.next_steps ?? new List<string>());
            _statusType = MessageType.Info;
            _statusMessage = USD_Loc.T("ai.resetToDraftDone");
        }

        private void ClearFinal()
        {
            _final = new USD_Annotation();
            _statusType = MessageType.Info;
            _statusMessage = USD_Loc.T("ai.cleared");
        }

        private void RunExplainableSummary(USD_Settings settings, string folder)
        {
            if (!ValidateFolder(folder, out var msg)) { ShowError(msg); return; }
            if (!USD_LlmClient.IsEnabled(settings)) { ShowError(USD_Loc.T("ai.errLlmDisabled")); return; }
            if (_busy) return;

            Debug.Log("[USD][AI] Explainable summary clicked");
            var report = SafeRead(Path.Combine(folder, "report.json"));
            var diff = SafeRead(Path.Combine(folder, "diff.json"));
            var delta = SafeRead(Path.Combine(folder, "deltaPatch.json"));
            var metricsBefore = SafeRead(Path.Combine(folder, "image_metrics_before.json"));
            var metricsAfter = SafeRead(Path.Combine(folder, "image_metrics_after.json"));

            _busy = true;
            _llmStatus.MarkSending("已发送请求（Explainable Summary）...");
            var messages = new List<(string role, string content)>
            {
                ("system", USD_AiPromptTemplates.ExplainSystem(settings.promptLanguage)),
                ("user", USD_AiPromptTemplates.ExplainUser(report, diff, delta, metricsBefore, metricsAfter, "{}"))
            };

            USD_LlmClient.ChatOnce(settings, messages,
                onOk: text =>
                {
                    var outPath = Path.Combine(folder, "report_summary_ai.md");
                    File.WriteAllText(outPath, text);
                    File.WriteAllText(Path.Combine(folder, "report_summary_ai_audit.json"), JsonUtility.ToJson(new AuditWrap { timestamp = DateTime.Now.ToString("s"), provider = settings.llmProvider, success = true, error = string.Empty, raw = text }, true));
                    AppendActionLog(folder, "generate_summary", "ok", string.Empty);
                    _statusType = MessageType.Info;
                    _statusMessage = USD_Loc.T("ai.summarySaved", outPath);
                    _llmStatus.MarkSuccess("AI 回复成功：已生成摘要。");
                    _lastOutputPath = outPath;
                    _busy = false;
                    AssetDatabase.Refresh();
                },
                onFail: err =>
                {
                    var outPath = Path.Combine(folder, "report_summary_ai.md");
                    File.WriteAllText(outPath, "# AI Summary
- Failed: " + err);
                    AppendActionLog(folder, "generate_summary", "fail", err);
                    _statusType = MessageType.Warning;
                    _statusMessage = USD_Loc.T("ai.summaryFailed", err);
                    _llmStatus.MarkFail(err);
                    _lastOutputPath = outPath;
                    _busy = false;
                    AssetDatabase.Refresh();
                });
        }

        private void RunRuleDraft(USD_Settings settings, string folder)
        {
            if (!ValidateFolder(folder, out var msg)) { ShowError(msg); return; }
            if (_busy) return;

            Debug.Log("[USD][AI] Rule draft clicked");
            var catalog = settings.labelCatalog != null ? settings.labelCatalog : USD_LabelCatalogUtil.GetOrCreateDefault();
            var selected = catalog.issues.Take(2).Select(x => x.id).ToList();
            var snap = SafeRead(Path.Combine(folder, "snapshot_after.json"));
            var delta = SafeRead(Path.Combine(folder, "deltaPatch.json"));
            var metrics = SafeRead(Path.Combine(folder, "image_metrics_after.json"));

            _busy = true;
            if (!USD_LlmClient.IsEnabled(settings))
            {
                var fallback = USD_AiPromptTemplates.ParseRuleDraftOrFallback(string.Empty, selected);
                fallback.source = "fallback-template";
                fallback.timestamp = DateTime.Now.ToString("s");
                fallback.error = USD_Loc.T("ai.errLlmDisabled");
                var fallbackDir = Path.Combine(folder, "Drafts").Replace('\', '/');
                USD_EditorUtil.EnsureFolder(fallbackDir);
                var fallbackPath = Path.Combine(fallbackDir, "rule_draft.json");
                File.WriteAllText(fallbackPath, JsonUtility.ToJson(fallback, true));
                AppendActionLog(folder, "rule_draft", "fail", fallback.error);
                _statusType = MessageType.Warning;
                _statusMessage = USD_Loc.T("ai.ruleWarn", fallback.error, fallbackPath);
                _llmStatus.MarkFail(fallback.error);
                _lastOutputPath = fallbackPath;
                _busy = false;
                AssetDatabase.Refresh();
                return;
            }

            _llmStatus.MarkSending("已发送请求（Rule Authoring Assist）...");
            var messages = new List<(string role, string content)>
            {
                ("system", USD_AiPromptTemplates.RuleSystem(settings.promptLanguage)),
                ("user", USD_AiPromptTemplates.RuleUser(selected, catalog, snap, delta, metrics))
            };

            USD_LlmClient.ChatOnce(settings, messages,
                onOk: text =>
                {
                    var draft = USD_AiPromptTemplates.ParseRuleDraftOrFallback(text, selected);
                    draft.source = "deepseek/openai-compatible";
                    draft.timestamp = DateTime.Now.ToString("s");
                    var draftDir = Path.Combine(folder, "Drafts").Replace('\', '/');
                    USD_EditorUtil.EnsureFolder(draftDir);
                    var outPath = Path.Combine(draftDir, "rule_draft.json");
                    File.WriteAllText(outPath, JsonUtility.ToJson(draft, true));
                    AppendActionLog(folder, "rule_draft", "ok", string.Empty);
                    _statusType = MessageType.Info;
                    _statusMessage = USD_Loc.T("ai.ruleSaved", outPath);
                    _llmStatus.MarkSuccess("AI 回复成功：已生成规则草案。");
                    _lastOutputPath = outPath;
                    _busy = false;
                    AssetDatabase.Refresh();
                },
                onFail: err =>
                {
                    var draft = USD_AiPromptTemplates.ParseRuleDraftOrFallback(string.Empty, selected);
                    draft.source = "deepseek/openai-compatible";
                    draft.timestamp = DateTime.Now.ToString("s");
                    draft.error = err;
                    var draftDir = Path.Combine(folder, "Drafts").Replace('\', '/');
                    USD_EditorUtil.EnsureFolder(draftDir);
                    var outPath = Path.Combine(draftDir, "rule_draft.json");
                    File.WriteAllText(outPath, JsonUtility.ToJson(draft, true));
                    AppendActionLog(folder, "rule_draft", "fail", err);
                    _statusType = MessageType.Warning;
                    _statusMessage = USD_Loc.T("ai.ruleWarn", err, outPath);
                    _llmStatus.MarkFail(err);
                    _lastOutputPath = outPath;
                    _busy = false;
                    AssetDatabase.Refresh();
                });
        }

        private void SavePairwise(string folder)
        {
            if (!ValidateFolder(folder, out var msg)) throw new Exception(msg);
            Debug.Log("[USD][AI] Pairwise save clicked");
            var pref = new USD_PairwisePreference
            {
                scene = Path.GetFileName(folder),
                styleA = _styleA,
                styleB = _styleB,
                choice = _pairChoice == 0 ? "A" : _pairChoice == 1 ? "B" : "Tie",
                reasons = new List<string>(_selectedReasonIds),
                timestamp = DateTime.Now.ToString("s")
            };
            var path = Path.Combine(folder, "pairwise_pref.json");
            File.WriteAllText(path, JsonUtility.ToJson(pref, true));
            AppendActionLog(folder, "pairwise", "ok", string.Empty);
            _statusType = MessageType.Info;
            _statusMessage = USD_Loc.T("ai.pairwiseSaved", path);
            _lastOutputPath = path;
            AssetDatabase.Refresh();
        }

        private static void WriteDraftFile(string folder, USD_Settings settings, USD_AiLabelDraft parsed, string rawText, string rawJson, string status, string error)
        {
            var file = new USD_AiDraftFile
            {
                timestamp = DateTime.Now.ToString("s"),
                provider = settings != null ? settings.llmProvider : "Off",
                model = settings != null ? settings.llmModel : string.Empty,
                raw_text = rawText,
                raw_json = rawJson,
                status = status,
                error = error,
                parsed = parsed
            };
            File.WriteAllText(Path.Combine(folder, DraftFileName), JsonUtility.ToJson(file, true));
        }

        private static void AppendActionLog(string folder, string action, string result, string error)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
            var row = JsonUtility.ToJson(new ActionRow
            {
                action = action,
                result = result,
                error = error,
                timestamp = DateTime.Now.ToString("s")
            });
            File.AppendAllText(Path.Combine(folder, ActionLogFileName), row + "\n");
        }

        private void UpdateDraftStatus(string status, string error)
        {
            var path = Path.Combine(_sampleFolder, DraftFileName);
            var draftFile = JsonUtility.FromJson<USD_AiDraftFile>(SafeRead(path)) ?? new USD_AiDraftFile();
            draftFile.status = status;
            if (!string.IsNullOrEmpty(error)) draftFile.error = error;
            if (_draft != null) draftFile.parsed = _draft;
            draftFile.timestamp = DateTime.Now.ToString("s");
            File.WriteAllText(path, JsonUtility.ToJson(draftFile, true));
        }

        private void UpdateBatchSummary(string aiStatus, USD_Annotation annotation)
        {
            if (string.IsNullOrWhiteSpace(_sampleFolder) || !Directory.Exists(_sampleFolder)) return;
            var dir = new DirectoryInfo(_sampleFolder);
            FileInfo summaryJson = null;
            for (var i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "batch_summary.json");
                if (File.Exists(candidate)) { summaryJson = new FileInfo(candidate); break; }
            }
            if (summaryJson == null) return;

            var summaryText = File.ReadAllText(summaryJson.FullName);
            var summary = JsonUtility.FromJson<BatchSummarySync>(summaryText);
            if (summary == null || summary.records == null) return;

            var sceneName = Path.GetFileName(_sampleFolder);
            var record = summary.records.FirstOrDefault(r => !string.IsNullOrEmpty(r.evidencePackPath) && NormalizePath(r.evidencePackPath) == NormalizePath(_sampleFolder));
            if (record == null) record = summary.records.FirstOrDefault(r => r.sceneName == sceneName);
            if (record == null) return;

            record.aiDraftStatus = aiStatus;
            record.userFinalLabel = $"{annotation.style_goal_id}:{annotation.score_1to10}:{string.Join("|", annotation.issue_tags ?? new List<string>())}";
            record.lastUpdated = DateTime.Now.ToString("s");
            File.WriteAllText(summaryJson.FullName, JsonUtility.ToJson(summary, true));

            var csvPath = Path.Combine(summaryJson.DirectoryName ?? string.Empty, "batch_summary.csv");
            if (File.Exists(csvPath))
            {
            var lines = new List<string> { "scenePath,sceneName,cameraMode,cameraName,styleGoalId,styleProfileName,beforeSnapshotPath,afterSnapshotPath,deltaPatchPath,evidencePackPath,keyVolumeDeltas,userRating,selectedIssues,aiDraftStatus,userFinalLabel,lastUpdated,status,failReason" };
                foreach (var r in summary.records)
                {
                    string Esc(string x) => '"' + (x ?? string.Empty).Replace("\"", "\"\"") + '"';
                    lines.Add(string.Join(",", new[]
                    {
                        Esc(r.scenePath), Esc(r.sceneName), Esc(r.cameraMode), Esc(r.cameraName), Esc(r.styleGoalId), Esc(r.styleProfileName), Esc(r.beforeSnapshotPath), Esc(r.afterSnapshotPath), Esc(r.deltaPatchPath), Esc(r.evidencePackPath),
                        Esc(string.Join("|", r.keyVolumeDeltas ?? new List<string>())), Esc(r.userRating.ToString()), Esc(string.Join("|", r.selectedIssues ?? new List<string>())), Esc(r.aiDraftStatus), Esc(r.userFinalLabel), Esc(r.lastUpdated), Esc(r.status), Esc(r.failReason)
                    }));
                }
                File.WriteAllLines(csvPath, lines);
            }
        }

        private void LoadDraftIfNeeded()
        {
            if (_draftLoaded) return;
            _draftLoaded = true;
            var path = Path.Combine(_sampleFolder ?? string.Empty, DraftFileName);
            if (!File.Exists(path)) return;
            var draftFile = JsonUtility.FromJson<USD_AiDraftFile>(SafeRead(path));
            _draft = draftFile != null && draftFile.parsed != null ? draftFile.parsed : null;
        }

        private void LoadFinalIfNeeded()
        {
            if (_finalLoaded) return;
            _finalLoaded = true;
            var path = Path.Combine(_sampleFolder ?? string.Empty, AnnotationFileName);
            if (!File.Exists(path)) return;
            _final = JsonUtility.FromJson<USD_Annotation>(SafeRead(path)) ?? new USD_Annotation();
        }

        private void ReloadLocalState()
        {
            _draft = null;
            _final = new USD_Annotation();
            _draftLoaded = false;
            _finalLoaded = false;
            _statusMessage = string.Empty;
            _lastOutputPath = string.Empty;
        }

        private void DrawLlmStatus()
        {
            if (_llmStatus.state == USD_LlmCallState.Idle) return;
            var msg = $"[{_llmStatus.state}] {_llmStatus.lastMessage}";
            if (!string.IsNullOrEmpty(_llmStatus.lastError)) msg += "\n" + _llmStatus.lastError;
            var type = _llmStatus.state == USD_LlmCallState.Fail ? MessageType.Error : MessageType.Info;
            EditorGUILayout.HelpBox(msg, type);
        }

        private void RunSafeAction(string action, Action fn)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                fn();
            }
            catch (Exception e)
            {
                Debug.LogError("[USD][AI] " + action + " failed: " + e);
                AppendActionLog(_sampleFolder, action, "fail", e.Message);
                ShowError(e.Message);
            }
            finally
            {
                _busy = false;
                AssetDatabase.Refresh();
            }
        }

        private void ShowError(string message)
        {
            _statusType = MessageType.Error;
            _statusMessage = USD_Loc.T("ai.failed", message);
            _lastOutputPath = _sampleFolder;
        }

        private static bool ValidateFolder(string folder, out string msg)
        {
            msg = string.Empty;
            if (string.IsNullOrWhiteSpace(folder)) { msg = USD_Loc.T("ai.folderNotSet"); return false; }
            if (!Directory.Exists(folder)) { msg = USD_Loc.T("ai.folderNotFound"); return false; }
            return true;
        }

        private static string SafeRead(string path) => File.Exists(path) ? File.ReadAllText(path) : "{}";

        private static string ToAssetPath(string abs)
        {
            var normalized = abs.Replace('\\', '/');
            var ap = Application.dataPath.Replace('\\', '/');
            return normalized.StartsWith(ap) ? "Assets" + normalized.Substring(ap.Length) : normalized;
        }

        private static string NormalizePath(string p) => (p ?? string.Empty).Replace('\\', '/').TrimEnd('/');

        [Serializable] private sealed class AuditWrap { public string timestamp; public string provider; public bool success; public string error; public string raw; }
        [Serializable] private sealed class ActionRow { public string action; public string result; public string error; public string timestamp; }
        [Serializable] private sealed class BatchSummarySync { public List<BatchRecordSync> records = new List<BatchRecordSync>(); }
        [Serializable]
        private sealed class BatchRecordSync
        {
            public string scenePath;
            public string sceneName;
            public string cameraMode;
            public string cameraName;
            public string styleGoalId;
            public string styleProfileName;
            public string beforeSnapshotPath;
            public string afterSnapshotPath;
            public string deltaPatchPath;
            public string evidencePackPath;
            public List<string> keyVolumeDeltas;
            public int userRating;
            public List<string> selectedIssues;
            public string aiDraftStatus;
            public string userFinalLabel;
            public string lastUpdated;
            public string status;
            public string failReason;
        }
    }
}
