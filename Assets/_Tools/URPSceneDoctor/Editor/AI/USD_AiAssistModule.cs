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

    public sealed class USD_AiAssistModule : IToolModule
    {
        public string ModuleName => "AI Assist";

        private string _sampleFolder;
        private int _pairChoice;
        private string _styleA = "Clean Stylized";
        private string _styleB = "Warm Dusk";
        private readonly List<string> _selectedReasonIds = new List<string>();

        public void DrawUI(USD_HubWindow hub)
        {
            var settings = USD_Settings.GetOrCreateSettings();
            var enabled = USD_LlmClient.IsEnabled(settings);
            EditorGUILayout.HelpBox(enabled ? USD_Loc.T("ai.enabled") : USD_Loc.T("ai.disabled"), enabled ? MessageType.Info : MessageType.Warning);
            EditorGUILayout.HelpBox(USD_Loc.T("help.ai.overview"), MessageType.Info);

            _sampleFolder = EditorGUILayout.TextField(USD_Loc.C("ai.sampleFolder"), _sampleFolder);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(USD_Loc.C("btn.pickFolder", "help.evidence.overview"), GUILayout.Width(120)))
            {
                var abs = EditorUtility.OpenFolderPanel(USD_Loc.T("btn.pickFolder"), Application.dataPath, "");
                if (!string.IsNullOrEmpty(abs)) _sampleFolder = ToAssetPath(abs);
            }
            if (GUILayout.Button(USD_Loc.C("btn.open"), GUILayout.Width(80)) && !string.IsNullOrEmpty(_sampleFolder)) EditorUtility.RevealInFinder(_sampleFolder);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            if (GUILayout.Button(USD_Loc.C("ai.draftLabel", "help.ai.overview"))) RunDraftLabeling(settings, _sampleFolder);
            if (GUILayout.Button(USD_Loc.C("ai.explainSummary"))) RunExplainableSummary(settings, _sampleFolder);
            if (GUILayout.Button(USD_Loc.C("ai.ruleAssist"))) RunRuleDraft(settings, _sampleFolder);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(USD_Loc.T("ai.pairwise"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(USD_Loc.T("help.pairwise"), MessageType.Info);
            _styleA = EditorGUILayout.TextField(USD_Loc.C("ai.styleA"), _styleA);
            _styleB = EditorGUILayout.TextField(USD_Loc.C("ai.styleB"), _styleB);
            _pairChoice = GUILayout.SelectionGrid(_pairChoice, new[] { USD_Loc.T("ai.aBetter"), USD_Loc.T("ai.bBetter"), USD_Loc.T("ai.tie") }, 3);

            var catalog = settings.labelCatalog != null ? settings.labelCatalog : USD_LabelCatalogUtil.GetOrCreateDefault();
            foreach (var issue in catalog.issues)
            {
                var on = _selectedReasonIds.Contains(issue.id);
                var next = EditorGUILayout.ToggleLeft(USD_LabelCatalogUtil.DisplayIssue(issue), on);
                if (next && !on) _selectedReasonIds.Add(issue.id);
                if (!next && on) _selectedReasonIds.Remove(issue.id);
            }

            if (GUILayout.Button(USD_Loc.C("ai.savePairwise"))) SavePairwise(_sampleFolder);
        }

        public USD_ModuleResult Execute(USD_RunContext context)
        {
            return new USD_ModuleResult { ModuleName = ModuleName, Snapshot = context.Snapshot ?? USD_AtmosScanner.CaptureSnapshot() };
        }

        public static USD_AiLabelDraft GenerateDraft(USD_Settings settings, string folder, string sceneName, USD_LabelCatalogAsset catalog, USD_ScanSnapshot before, USD_ScanSnapshot after, USD_DeltaPatch patch, USD_ImageMetricsFile metricsBefore, USD_ImageMetricsFile metricsAfter)
        {
            USD_AiLabelDraft draft;
            if (USD_LlmClient.IsEnabled(settings))
            {
                var res = USD_LlmClient.Chat(settings,
                    USD_AiPromptTemplates.DraftLabelingSystem(settings.promptLanguage),
                    USD_AiPromptTemplates.DraftLabelingUser(catalog, sceneName, after, patch, metricsBefore, metricsAfter));
                draft = res.success
                    ? USD_AiPromptTemplates.ParseLabelDraftOrFallback(res.text, catalog, metricsAfter)
                    : USD_AiPromptTemplates.ParseLabelDraftOrFallback(string.Empty, catalog, metricsAfter);
                draft.error = res.success ? draft.error : res.error;
                File.WriteAllText(folder + "/ai_label_draft_raw.json", res.raw_json ?? "");
            }
            else
            {
                draft = USD_AiPromptTemplates.ParseLabelDraftOrFallback(string.Empty, catalog, metricsAfter);
                draft.error = "LLM disabled";
            }

            draft.source = USD_LlmClient.IsEnabled(settings) ? "deepseek/openai-compatible" : "fallback-template";
            draft.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            File.WriteAllText(folder + "/ai_label_draft.json", JsonUtility.ToJson(draft, true));
            return draft;
        }

        private static void ApplyDraftToTaste(string folder, USD_AiLabelDraft draft)
        {
            var notePath = folder + "/taste_note.json";
            if (!File.Exists(notePath) || draft == null) return;
            var json = File.ReadAllText(notePath);
            var note = JsonUtility.FromJson<BatchNoteProxy>(json);
            if (note == null) return;
            note.goal.styleGoalId = draft.recommended_style_goal_id;
            note.after_evaluation.score = draft.recommended_score_1to10;
            note.before_issues = draft.recommended_issue_tags_top3 ?? new List<string>();
            File.WriteAllText(notePath, JsonUtility.ToJson(note, true));
        }

        private void RunDraftLabeling(USD_Settings settings, string folder)
        {
            if (!ValidateFolder(folder, out var msg)) { EditorUtility.DisplayDialog(USD_Loc.T("ai.dialogTitle"), msg, USD_Loc.T("common.ok")); return; }
            var catalog = settings.labelCatalog != null ? settings.labelCatalog : USD_LabelCatalogUtil.GetOrCreateDefault();
            var sceneName = Path.GetFileName(folder);
            var before = JsonUtility.FromJson<USD_ScanSnapshot>(SafeRead(folder + "/snapshot_before.json")) ?? new USD_ScanSnapshot();
            var after = JsonUtility.FromJson<USD_ScanSnapshot>(SafeRead(folder + "/snapshot_after.json")) ?? new USD_ScanSnapshot();
            var patch = JsonUtility.FromJson<USD_DeltaPatch>(SafeRead(folder + "/deltaPatch.json")) ?? new USD_DeltaPatch();
            var mBefore = JsonUtility.FromJson<USD_ImageMetricsFile>(SafeRead(folder + "/image_metrics_before.json")) ?? new USD_ImageMetricsFile();
            var mAfter = JsonUtility.FromJson<USD_ImageMetricsFile>(SafeRead(folder + "/image_metrics_after.json")) ?? new USD_ImageMetricsFile();
            var draft = GenerateDraft(settings, folder, sceneName, catalog, before, after, patch, mBefore, mAfter);

            var action = EditorUtility.DisplayDialogComplex(USD_Loc.T("ai.draftLabel"), JsonUtility.ToJson(draft, true), USD_Loc.T("batch.acceptAi"), USD_Loc.T("batch.rejectAi"), USD_Loc.T("btn.open"));
            if (action == 0) ApplyDraftToTaste(folder, draft);
            if (action == 1)
            {
                draft.recommended_issue_tags_top3 = new List<string>();
                draft.short_reason = "Rejected by user";
                File.WriteAllText(folder + "/ai_label_draft.json", JsonUtility.ToJson(draft, true));
            }
            AssetDatabase.Refresh();
        }

        private void RunExplainableSummary(USD_Settings settings, string folder)
        {
            if (!ValidateFolder(folder, out var msg)) { EditorUtility.DisplayDialog(USD_Loc.T("ai.dialogTitle"), msg, USD_Loc.T("common.ok")); return; }
            if (!USD_LlmClient.IsEnabled(settings)) return;
            var report = SafeRead(folder + "/report.json");
            var diff = SafeRead(folder + "/diff.json");
            var delta = SafeRead(folder + "/deltaPatch.json");
            var metricsBefore = SafeRead(folder + "/image_metrics_before.json");
            var metricsAfter = SafeRead(folder + "/image_metrics_after.json");
            var policy = "{}";
            var res = USD_LlmClient.Chat(settings, USD_AiPromptTemplates.ExplainSystem(settings.promptLanguage), USD_AiPromptTemplates.ExplainUser(report, diff, delta, metricsBefore, metricsAfter, policy));
            var outPath = folder + "/report_summary_ai.md";
            var text = res.success ? res.text : "# AI Summary\n- Failed: " + res.error;
            File.WriteAllText(outPath, text);
            File.WriteAllText(folder + "/report_summary_ai_audit.json", JsonUtility.ToJson(new AuditWrap { timestamp = DateTime.Now.ToString("s"), provider = settings.llmProvider, success = res.success, error = res.error, raw = res.raw_json }, true));
            AssetDatabase.Refresh();
        }

        private void RunRuleDraft(USD_Settings settings, string folder)
        {
            if (!ValidateFolder(folder, out var msg)) { EditorUtility.DisplayDialog(USD_Loc.T("ai.dialogTitle"), msg, USD_Loc.T("common.ok")); return; }
            var catalog = settings.labelCatalog != null ? settings.labelCatalog : USD_LabelCatalogUtil.GetOrCreateDefault();
            var selected = catalog.issues.Take(2).Select(x => x.id).ToList();
            var snap = SafeRead(folder + "/snapshot_after.json");
            var delta = SafeRead(folder + "/deltaPatch.json");
            var metrics = SafeRead(folder + "/image_metrics_after.json");
            USD_RuleDraft draft;
            if (USD_LlmClient.IsEnabled(settings))
            {
                var res = USD_LlmClient.Chat(settings, USD_AiPromptTemplates.RuleSystem(settings.promptLanguage), USD_AiPromptTemplates.RuleUser(selected, catalog, snap, delta, metrics));
                draft = res.success ? USD_AiPromptTemplates.ParseRuleDraftOrFallback(res.text, selected) : USD_AiPromptTemplates.ParseRuleDraftOrFallback(string.Empty, selected);
                draft.error = res.success ? draft.error : res.error;
                File.WriteAllText(folder + "/rule_draft_raw.json", res.raw_json ?? "");
            }
            else
            {
                draft = USD_AiPromptTemplates.ParseRuleDraftOrFallback(string.Empty, selected);
                draft.error = "LLM disabled";
            }

            draft.source = USD_LlmClient.IsEnabled(settings) ? "deepseek/openai-compatible" : "fallback-template";
            draft.timestamp = DateTime.Now.ToString("s");
            var draftDir = folder + "/Drafts";
            USD_EditorUtil.EnsureFolder(draftDir);
            File.WriteAllText(draftDir + "/rule_draft.json", JsonUtility.ToJson(draft, true));
            AssetDatabase.Refresh();
        }

        private void SavePairwise(string folder)
        {
            if (!ValidateFolder(folder, out _)) return;
            var pref = new USD_PairwisePreference
            {
                scene = Path.GetFileName(folder),
                styleA = _styleA,
                styleB = _styleB,
                choice = _pairChoice == 0 ? "A" : _pairChoice == 1 ? "B" : "Tie",
                reasons = new List<string>(_selectedReasonIds),
                timestamp = DateTime.Now.ToString("s")
            };
            File.WriteAllText(folder + "/pairwise_pref.json", JsonUtility.ToJson(pref, true));
            AssetDatabase.Refresh();
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

        [Serializable] private sealed class AuditWrap { public string timestamp; public string provider; public bool success; public string error; public string raw; }
        [Serializable] private sealed class BatchNoteProxyGoal { public string styleGoalId; public string styleProfileName; }
        [Serializable] private sealed class BatchNoteProxyEval { public int score; public List<string> what_improved; public List<string> what_still_bad; }
        [Serializable] private sealed class BatchNoteProxy { public string scene; public List<string> tags; public BatchNoteProxyGoal goal; public List<string> before_issues; public List<object> actions; public BatchNoteProxyEval after_evaluation; }
    }
}
