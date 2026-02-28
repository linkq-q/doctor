using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    [Serializable]
    public sealed class USD_BatchSampleRecord
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
        public List<string> keyVolumeDeltas = new List<string>();
        public int userRating;
        public List<string> selectedIssues = new List<string>();
        public string aiDraftStatus;
        public string userFinalLabel;
        public string status;
        public string failReason;
    }

    [Serializable]
    public sealed class USD_BatchSummary
    {
        public string runId;
        public List<USD_BatchSampleRecord> records = new List<USD_BatchSampleRecord>();
    }

    public sealed class USD_BatchSamplerModule : IToolModule
    {
        public string ModuleName => "Batch Sampler";

        private string _filter = string.Empty;
        private Vector2 _scroll;
        private readonly Dictionary<string, bool> _sceneSelection = new Dictionary<string, bool>();
        private USD_BatchSummary _lastSummary;
        private string _lastSummaryPath;
        private int _selectedStyleGoal;
        private int _selectedRecord;
        private int _rating = 5;
        private readonly Dictionary<string, bool> _issueToggles = new Dictionary<string, bool>();

        public void DrawUI(USD_HubWindow hub)
        {
            var settings = USD_Settings.GetOrCreateSettings();
            var catalog = settings.labelCatalog != null ? settings.labelCatalog : USD_LabelCatalogUtil.GetOrCreateDefault();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(USD_Loc.C("batch.refreshScenes", "help.batch.overview"), GUILayout.Width(120))) RefreshScenes(settings.batchSceneRoot);
            if (GUILayout.Button(USD_Localization.T("btn.batchrun"), GUILayout.Width(120))) RunBatch(hub, catalog, settings);
            if (GUILayout.Button(USD_Loc.C("batch.openRoot"), GUILayout.Width(120))) EditorUtility.RevealInFinder("Assets/_Tools/URPSceneDoctor/BatchRuns");
            EditorGUILayout.EndHorizontal();

            _filter = EditorGUILayout.TextField(USD_Loc.C("batch.filter"), _filter);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(USD_Loc.C("batch.selectAll"), GUILayout.Width(90))) SetAll(true);
            if (GUILayout.Button(USD_Loc.C("batch.clear"), GUILayout.Width(90))) SetAll(false);
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(180));
            foreach (var kv in _sceneSelection.Keys.OrderBy(x => x).ToList())
            {
                if (!string.IsNullOrEmpty(_filter) && kv.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                _sceneSelection[kv] = EditorGUILayout.ToggleLeft(kv, _sceneSelection[kv]);
            }
            EditorGUILayout.EndScrollView();

            if (_lastSummary != null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField(USD_Loc.T("batch.lastSummary"), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(USD_Loc.T("batch.summaryPath"), _lastSummaryPath ?? USD_Loc.T("common.none"));
                var labels = _lastSummary.records.Select(r => $"{r.sceneName} [{r.status}] {USD_Loc.T("batch.rating")}={r.userRating}").ToArray();
                if (labels.Length > 0)
                {
                    _selectedRecord = Mathf.Clamp(_selectedRecord, 0, labels.Length - 1);
                    _selectedRecord = EditorGUILayout.Popup(USD_Loc.T("batch.sample"), _selectedRecord, labels);
                    DrawAnnotationEditor(catalog, _lastSummary.records[_selectedRecord]);
                }
            }
        }

        public USD_ModuleResult Execute(USD_RunContext context)
        {
            return new USD_ModuleResult { ModuleName = ModuleName, Snapshot = context.Snapshot ?? USD_AtmosScanner.CaptureSnapshot() };
        }

        private void RefreshScenes(string root)
        {
            _sceneSelection.Clear();
            var searchRoot = string.IsNullOrWhiteSpace(root) ? "Assets/_Tools/URPSceneDoctor/SamplePacks" : root;
            if (!AssetDatabase.IsValidFolder(searchRoot)) return;
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { searchRoot });
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) _sceneSelection[p] = false;
            }
        }

        private void SetAll(bool value)
        {
            foreach (var k in _sceneSelection.Keys.ToList()) _sceneSelection[k] = value;
        }

        private void RunBatch(USD_HubWindow hub, USD_LabelCatalogAsset catalog, USD_Settings settings)
        {
            var selected = _sceneSelection.Where(x => x.Value).Select(x => x.Key).ToList();
            if (selected.Count == 0)
            {
                EditorUtility.DisplayDialog(USD_Loc.T("tab.batch"), USD_Loc.T("batch.noScene"), USD_Loc.T("common.ok"));
                return;
            }

            var runId = USD_EditorUtil.Timestamp;
            var root = $"Assets/_Tools/URPSceneDoctor/BatchRuns/{runId}";
            USD_EditorUtil.EnsureFolder("Assets/_Tools/URPSceneDoctor/BatchRuns");
            USD_EditorUtil.EnsureFolder(root);
            USD_EditorUtil.EnsureFolder(root + "/Samples");

            var summary = new USD_BatchSummary { runId = runId };
            var errors = new StringBuilder();

            var styleGoal = catalog.styles.Count > 0 ? catalog.styles[Mathf.Clamp(_selectedStyleGoal, 0, catalog.styles.Count - 1)] : null;

            foreach (var scenePath in selected)
            {
                var rec = new USD_BatchSampleRecord { scenePath = scenePath, sceneName = Path.GetFileNameWithoutExtension(scenePath), styleGoalId = styleGoal != null ? styleGoal.id : "" };
                summary.records.Add(rec);
                try
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                    var sampleTs = USD_EditorUtil.Timestamp;
                    var sampleDir = $"{root}/Samples/{rec.sceneName}/{sampleTs}";
                    USD_EditorUtil.EnsureFolder($"{root}/Samples/{rec.sceneName}");
                    USD_EditorUtil.EnsureFolder(sampleDir);
                    USD_EditorUtil.EnsureFolder(sampleDir + "/before");
                    USD_EditorUtil.EnsureFolder(sampleDir + "/after");

                    var pick = USD_CameraPicker.PickAutoCamera();
                    rec.cameraMode = "Auto";
                    rec.cameraName = pick.camera != null ? pick.camera.name : "SceneView";

                    var before = USD_AtmosScanner.CaptureSnapshot();
                    rec.beforeSnapshotPath = sampleDir + "/snapshot_before.json";
                    File.WriteAllText(rec.beforeSnapshotPath, JsonUtility.ToJson(before, true));
                    var beforeShots = USD_ScreenshotUtil.CaptureSixShots(sampleDir + "/before", settings.screenshotWidth, settings.screenshotHeight, pick.camera);

                    var styleProfile = ResolveStyleProfile(styleGoal, hub);
                    rec.styleProfileName = styleProfile != null ? styleProfile.profileName : "";
                    var applyChanges = USD_PatchApplier.ApplyPatch(rec.sceneName, sampleTs, USD_ApplyMode.VisibleDemo, styleProfile, true);

                    var after = USD_AtmosScanner.CaptureSnapshot();
                    rec.afterSnapshotPath = sampleDir + "/snapshot_after.json";
                    File.WriteAllText(rec.afterSnapshotPath, JsonUtility.ToJson(after, true));
                    var afterShots = USD_ScreenshotUtil.CaptureSixShots(sampleDir + "/after", settings.screenshotWidth, settings.screenshotHeight, pick.camera);

                    var metricsBefore = USD_VisionLiteUtil.BuildMetrics(rec.sceneName, sampleTs, sampleDir, beforeShots);
                    var metricsAfter = USD_VisionLiteUtil.BuildMetrics(rec.sceneName, sampleTs, sampleDir, afterShots);
                    File.WriteAllText(sampleDir + "/image_metrics_before.json", JsonUtility.ToJson(metricsBefore, true));
                    File.WriteAllText(sampleDir + "/image_metrics_after.json", JsonUtility.ToJson(metricsAfter, true));
                    File.WriteAllText(sampleDir + "/image_metrics_diff.json", JsonUtility.ToJson(USD_VisionLiteUtil.BuildDiff(metricsBefore, metricsAfter), true));

                    var patch = USD_DeltaExtractor.Extract(rec.sceneName, rec.beforeSnapshotPath, rec.afterSnapshotPath);
                    rec.deltaPatchPath = sampleDir + "/deltaPatch.json";
                    File.WriteAllText(rec.deltaPatchPath, JsonUtility.ToJson(patch, true));
                    rec.keyVolumeDeltas = patch != null ? patch.changedFields.Where(x => x.path.StartsWith("VolumeKey.")).Take(6).Select(x => x.path + ": " + x.deltaHint).ToList() : new List<string>();

                    WriteTasteNote(sampleDir + "/taste_note.json", rec, patch);

                    var aiDraft = USD_AiAssistModule.GenerateDraft(settings, sampleDir, rec.sceneName, catalog, before, after, patch, metricsBefore, metricsAfter);
                    rec.aiDraftStatus = string.IsNullOrEmpty(aiDraft.error) ? "generated" : "fallback:" + aiDraft.error;

                    var summaryMd = new StringBuilder();
                    summaryMd.AppendLine("# Batch Sample Evidence");
                    summaryMd.AppendLine("## Apply Changes");
                    applyChanges.ForEach(c => summaryMd.AppendLine("- " + c));
                    rec.evidencePackPath = sampleDir;
                    File.WriteAllText(sampleDir + "/summary.md", summaryMd.ToString());

                    rec.status = "success";
                    rec.failReason = string.Empty;
                }
                catch (Exception e)
                {
                    rec.status = "fail";
                    rec.failReason = e.Message;
                    errors.AppendLine(scenePath + " => " + e);
                }
            }

            _lastSummary = summary;
            _lastSummaryPath = root + "/batch_summary.json";
            File.WriteAllText(_lastSummaryPath, JsonUtility.ToJson(summary, true));
            File.WriteAllText(root + "/batch_summary.csv", BuildCsv(summary));
            if (errors.Length > 0) File.WriteAllText(root + "/errors.log", errors.ToString());
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(USD_Loc.T("tab.batch"), USD_Loc.T("batch.done", root), USD_Loc.T("common.ok"));
        }

        private static USD_StyleProfileAsset ResolveStyleProfile(USD_StyleGoal goal, USD_HubWindow hub)
        {
            if (goal == null) return hub.SelectedStyleProfile;
            var builtins = USD_StyleProfileUtil.GetOrCreateBuiltIns();
            return builtins.FirstOrDefault(x => x != null && x.profileName == goal.defaultStyleProfileId) ?? hub.SelectedStyleProfile;
        }

        private void DrawAnnotationEditor(USD_LabelCatalogAsset catalog, USD_BatchSampleRecord rec)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(USD_Loc.T("batch.quickLabel"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(USD_Loc.T("help.batch.overview"), MessageType.Info);
            if (!string.IsNullOrEmpty(rec.evidencePackPath))
            {
                var draftPath = rec.evidencePackPath + "/ai_label_draft.json";
                if (File.Exists(draftPath))
                {
                    var draft = USD_AiAssistModule.LoadParsedDraft(draftPath);
                    EditorGUILayout.HelpBox(USD_Loc.T("batch.aiDraft", draft != null ? draft.short_reason : USD_Loc.T("common.invalid")), MessageType.None);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(USD_Loc.C("batch.acceptAi"), GUILayout.Width(140)) && draft != null)
                    {
                        rec.styleGoalId = draft.recommended_style_goal_id;
                        rec.userRating = draft.recommended_score_1to10;
                        rec.selectedIssues = draft.recommended_issue_tags_top3 != null ? new List<string>(draft.recommended_issue_tags_top3) : new List<string>();
                        rec.userFinalLabel = "accepted_ai";
                    }
                    if (GUILayout.Button(USD_Loc.C("batch.rejectAi"), GUILayout.Width(140)))
                    {
                        rec.userFinalLabel = "rejected_ai";
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            var styleNames = catalog.styles.Select(USD_LabelCatalogUtil.DisplayStyle).ToArray();
            if (styleNames.Length > 0)
            {
                var idx = Mathf.Max(0, catalog.styles.FindIndex(x => x.id == rec.styleGoalId));
                idx = EditorGUILayout.Popup(USD_Loc.T("batch.styleGoal"), idx, styleNames);
                rec.styleGoalId = catalog.styles[idx].id;
            }

            _rating = EditorGUILayout.IntSlider(USD_Loc.T("batch.rating"), rec.userRating <= 0 ? _rating : rec.userRating, 1, 10);
            rec.userRating = _rating;

            foreach (var issue in catalog.issues)
            {
                var on = rec.selectedIssues.Contains(issue.id);
                var newOn = EditorGUILayout.ToggleLeft(USD_LabelCatalogUtil.DisplayIssue(issue), on);
                if (newOn && !on) rec.selectedIssues.Add(issue.id);
                if (!newOn && on) rec.selectedIssues.Remove(issue.id);
            }

            if (GUILayout.Button(USD_Loc.C("batch.saveAnnotation")))
            {
                if (!string.IsNullOrEmpty(rec.evidencePackPath))
                {
                    var notePath = rec.evidencePackPath + "/taste_note.json";
                    SaveAnnotationToTasteNote(notePath, rec);
                    rec.userFinalLabel = rec.styleGoalId + ":" + rec.userRating;
                    if (!string.IsNullOrEmpty(_lastSummaryPath))
                    {
                        File.WriteAllText(_lastSummaryPath, JsonUtility.ToJson(_lastSummary, true));
                        File.WriteAllText(Path.GetDirectoryName(_lastSummaryPath) + "/batch_summary.csv", BuildCsv(_lastSummary));
                    }
                    AssetDatabase.Refresh();
                }
            }
        }

        private static void WriteTasteNote(string path, USD_BatchSampleRecord rec, USD_DeltaPatch patch)
        {
            var actions = patch != null ? patch.changedFields.Where(x => x.path.StartsWith("VolumeKey.")).Select(x => new BatchAction
            {
                knob = x.path,
                before = x.before,
                after = x.after,
                delta = x.deltaHint,
                intent = "",
                stop_rule = ""
            }).ToList() : new List<BatchAction>();

            var note = new BatchTasteNote
            {
                scene = rec.sceneName,
                tags = new List<string>(),
                goal = new BatchGoal { styleGoalId = rec.styleGoalId, styleProfileName = rec.styleProfileName },
                before_issues = new List<string>(),
                actions = actions,
                after_evaluation = new BatchEval { score = -1, what_improved = new List<string>(), what_still_bad = new List<string>() }
            };
            File.WriteAllText(path, JsonUtility.ToJson(note, true));
        }

        private static void SaveAnnotationToTasteNote(string path, USD_BatchSampleRecord rec)
        {
            if (!File.Exists(path)) return;
            var note = JsonUtility.FromJson<BatchTasteNote>(File.ReadAllText(path));
            if (note == null) return;
            note.goal.styleGoalId = rec.styleGoalId;
            note.after_evaluation.score = rec.userRating;
            note.before_issues = rec.selectedIssues;
            File.WriteAllText(path, JsonUtility.ToJson(note, true));
        }

        private static string BuildCsv(USD_BatchSummary summary)
        {
            var sb = new StringBuilder();
            sb.AppendLine("scenePath,sceneName,cameraMode,cameraName,styleGoalId,styleProfileName,beforeSnapshotPath,afterSnapshotPath,deltaPatchPath,evidencePackPath,keyVolumeDeltas,userRating,selectedIssues,aiDraftStatus,userFinalLabel,status,failReason");
            foreach (var r in summary.records)
            {
                string Escape(string s) => '"' + (s ?? string.Empty).Replace("\"", "\"\"") + '"';
                sb.AppendLine(string.Join(",", new[]
                {
                    Escape(r.scenePath), Escape(r.sceneName), Escape(r.cameraMode), Escape(r.cameraName), Escape(r.styleGoalId), Escape(r.styleProfileName),
                    Escape(r.beforeSnapshotPath), Escape(r.afterSnapshotPath), Escape(r.deltaPatchPath), Escape(r.evidencePackPath), Escape(string.Join("|", r.keyVolumeDeltas)),
                    Escape(r.userRating.ToString()), Escape(string.Join("|", r.selectedIssues)), Escape(r.aiDraftStatus), Escape(r.userFinalLabel), Escape(r.status), Escape(r.failReason)
                }));
            }
            return sb.ToString();
        }

        [Serializable]
        private sealed class BatchGoal { public string styleGoalId; public string styleProfileName; }
        [Serializable]
        private sealed class BatchAction { public string knob; public string before; public string after; public string delta; public string intent; public string stop_rule; }
        [Serializable]
        private sealed class BatchEval { public int score = -1; public List<string> what_improved; public List<string> what_still_bad; }
        [Serializable]
        private sealed class BatchTasteNote
        {
            public string scene;
            public List<string> tags;
            public BatchGoal goal;
            public List<string> before_issues;
            public List<BatchAction> actions;
            public BatchEval after_evaluation;
        }
    }
}
