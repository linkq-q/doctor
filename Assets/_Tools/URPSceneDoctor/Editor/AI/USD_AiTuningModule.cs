using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPSceneDoctor.Editor
{
    [Serializable]
    internal sealed class USD_AiTuningProposalVariant
    {
        public USD_AiTuningParams @params = new USD_AiTuningParams();
        public string rationale;
    }

    [Serializable]
    internal sealed class USD_AiTuningProposal
    {
        public USD_AiTuningProposalVariant variantA = new USD_AiTuningProposalVariant();
        public USD_AiTuningProposalVariant variantB = new USD_AiTuningProposalVariant();
        public List<string> notes = new List<string>();
    }

    [Serializable]
    internal sealed class USD_AiTuningParams
    {
        public float Bloom_intensity;
        public float Bloom_scatter = 0.5f;
        public float WB_temperature;
        public float WB_tint;
        public float Vig_intensity = 0.2f;
        public float Vig_smoothness = 0.2f;
        public float Grain_intensity = 0.6f;

        public Dictionary<string, float> ToMap()
        {
            return new Dictionary<string, float>
            {
                {"Bloom.intensity", Bloom_intensity},
                {"Bloom.scatter", Bloom_scatter},
                {"WB.temperature", WB_temperature},
                {"WB.tint", WB_tint},
                {"Vig.intensity", Vig_intensity},
                {"Vig.smoothness", Vig_smoothness},
                {"Grain.intensity", Grain_intensity}
            };
        }

        public void FromMap(Dictionary<string, float> map)
        {
            Bloom_intensity = Get(map, "Bloom.intensity", 0f);
            Bloom_scatter = Get(map, "Bloom.scatter", 0.5f);
            WB_temperature = Get(map, "WB.temperature", 0f);
            WB_tint = Get(map, "WB.tint", 0f);
            Vig_intensity = Get(map, "Vig.intensity", 0.2f);
            Vig_smoothness = Get(map, "Vig.smoothness", 0.2f);
            Grain_intensity = Get(map, "Grain.intensity", 0.6f);
        }

        private static float Get(Dictionary<string, float> map, string key, float fallback)
        {
            return map != null && map.TryGetValue(key, out var v) ? v : fallback;
        }
    }

    public sealed class USD_AiTuningModule : IToolModule
    {
        public string ModuleName => "AI Tuning";

        private string _runRoot;
        private string _status;
        private MessageType _statusType = MessageType.Info;
        private int _targetStyleIndex;
        private bool _useMetricsGoal = true;
        private float _goalOverExposure = 0.01f;
        private float _goalCenterContrast = 1.1f;
        private float _bloomCeiling = 5f;

        private string _baseFolder;
        private USD_ImageMetricsFile _baseMetrics;
        private USD_ScanSnapshot _baseSnapshot;
        private USD_AiTuningProposal _proposal;

        private readonly VariantState _variantA = new VariantState("A");
        private readonly VariantState _variantB = new VariantState("B");
        private int _pairwiseChoice;

        private sealed class VariantState
        {
            public readonly string Name;
            public readonly USD_AiTuningParams Params = new USD_AiTuningParams();
            public int Score = 5;
            public List<string> Tags = new List<string>();
            public string Folder;
            public VariantState(string name) { Name = name; }
        }

        public void DrawUI(USD_HubWindow hub)
        {
            var settings = USD_Settings.GetOrCreateSettings();
            var catalog = settings.labelCatalog != null ? settings.labelCatalog : USD_LabelCatalogUtil.GetOrCreateDefault();

            EditorGUILayout.HelpBox(USD_Loc.T("aituning.overview"), MessageType.Info);

            if (catalog == null || catalog.styles == null || catalog.styles.Count == 0)
            {
                EditorGUILayout.HelpBox(USD_Loc.T("ai.errNoCatalog"), MessageType.Error);
                return;
            }

            _targetStyleIndex = Mathf.Clamp(_targetStyleIndex, 0, Mathf.Max(0, catalog.styles.Count - 1));
            var styleLabels = catalog.styles.Select(GetStyleDisplay).ToArray();
            _targetStyleIndex = EditorGUILayout.Popup(USD_Loc.T("aituning.targetStyle"), _targetStyleIndex, styleLabels);
            _useMetricsGoal = EditorGUILayout.ToggleLeft(USD_Loc.T("aituning.useMetricsGoal"), _useMetricsGoal);
            if (_useMetricsGoal)
            {
                EditorGUI.indentLevel++;
                _goalOverExposure = EditorGUILayout.Slider("overexposure_ratio <", _goalOverExposure, 0f, 0.2f);
                _goalCenterContrast = EditorGUILayout.Slider("center_contrast_ratio >", _goalCenterContrast, 0.8f, 1.5f);
                _bloomCeiling = EditorGUILayout.Slider("bloomCeiling", _bloomCeiling, 1f, 10f);
                EditorGUI.indentLevel--;
            }

            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, _statusType);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(USD_Loc.C("aituning.captureBase"))) CaptureBase(hub);
            if (GUILayout.Button(USD_Loc.C("aituning.propose"))) Propose(hub, settings, catalog);
            if (GUILayout.Button(USD_Loc.C("aituning.runBoth"))) { RunVariant(hub, _variantA); RunVariant(hub, _variantB); }
            EditorGUILayout.EndHorizontal();

            DrawVariantCard(hub, catalog, _variantA, _proposal?.variantA?.rationale);
            DrawVariantCard(hub, catalog, _variantB, _proposal?.variantB?.rationale);
            DrawJudgePanel(catalog);

            if (!string.IsNullOrEmpty(_runRoot))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(USD_Loc.C("ai.openFolder"))) EditorUtility.RevealInFinder(_runRoot);
                EditorGUILayout.LabelField(_runRoot, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
        }

        public USD_ModuleResult Execute(USD_RunContext context)
        {
            return new USD_ModuleResult { ModuleName = ModuleName, Snapshot = context.Snapshot ?? USD_AtmosScanner.CaptureSnapshot() };
        }

        private void CaptureBase(USD_HubWindow hub)
        {
            try
            {
                var runId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _runRoot = $"Assets/_Tools/URPSceneDoctor/AITuningRuns/{runId}";
                USD_EditorUtil.EnsureFolder("Assets/_Tools/URPSceneDoctor/AITuningRuns");
                USD_EditorUtil.EnsureFolder(_runRoot);
                _baseFolder = _runRoot + "/base";
                USD_EditorUtil.EnsureFolder(_baseFolder);
                USD_EditorUtil.EnsureFolder(_baseFolder + "/screenshots_before");

                _baseSnapshot = USD_AtmosScanner.CaptureSnapshot();
                File.WriteAllText(_baseFolder + "/snapshot_before.json", JsonUtility.ToJson(_baseSnapshot, true));
                var beforeShots = USD_ScreenshotUtil.CaptureSixShots(_baseFolder + "/screenshots_before", hub.ScreenshotWidth, hub.ScreenshotHeight, null);
                _baseMetrics = USD_VisionLiteUtil.BuildMetrics(hub.ActiveSceneName, USD_EditorUtil.Timestamp, _runRoot, beforeShots);
                File.WriteAllText(_baseFolder + "/image_metrics_before.json", JsonUtility.ToJson(_baseMetrics, true));
                SetStatus(MessageType.Info, $"Base captured: {_baseFolder}");
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                SetStatus(MessageType.Error, "Capture Base failed: " + e.Message);
            }
        }

        private void Propose(USD_HubWindow hub, USD_Settings settings, USD_LabelCatalogAsset catalog)
        {
            if (_baseSnapshot == null || _baseMetrics == null)
            {
                SetStatus(MessageType.Warning, USD_Loc.T("aituning.needBase"));
                return;
            }

            try
            {
                var styleId = catalog.styles[_targetStyleIndex].id;
                _proposal = BuildFallbackProposal(styleId, _baseMetrics.aggregate);
                if (USD_LlmClient.IsEnabled(settings))
                {
                    var prompt = BuildProposalPrompt(styleId);
                    var llm = USD_LlmClient.Chat(settings, USD_Loc.T("aituning.proposeSystemPrompt"), prompt);
                    if (llm.success)
                    {
                        var parsed = TryParseProposal(llm.text);
                        if (parsed != null) _proposal = parsed;
                    }
                }

                ClampAndNormalize(_proposal.variantA.@params, _baseMetrics.aggregate);
                ClampAndNormalize(_proposal.variantB.@params, _baseMetrics.aggregate);
                EnsureVariantDifference(_proposal);

                _variantA.Params.FromMap(_proposal.variantA.@params.ToMap());
                _variantB.Params.FromMap(_proposal.variantB.@params.ToMap());

                var proposalDir = _runRoot + "/proposal";
                USD_EditorUtil.EnsureFolder(proposalDir);
                File.WriteAllText(proposalDir + "/ai_param_proposal.json", ToProposalJson(_proposal));
                SetStatus(MessageType.Info, "Proposal generated: " + proposalDir + "/ai_param_proposal.json");
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                SetStatus(MessageType.Error, "Propose failed: " + e.Message);
            }
        }

        private void DrawVariantCard(USD_HubWindow hub, USD_LabelCatalogAsset catalog, VariantState state, string rationale)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Variant {state.Name}", EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(rationale)) EditorGUILayout.HelpBox(rationale, MessageType.None);

            DrawParamField(state.Params, "Bloom.intensity", v => state.Params.Bloom_intensity = v, 0, _bloomCeiling);
            DrawParamField(state.Params, "Bloom.scatter", v => state.Params.Bloom_scatter = v, 0, 1);
            DrawParamField(state.Params, "WB.temperature", v => state.Params.WB_temperature = v, -20, 20);
            DrawParamField(state.Params, "WB.tint", v => state.Params.WB_tint = v, -10, 10);
            DrawParamField(state.Params, "Vig.intensity", v => state.Params.Vig_intensity = v, 0, 0.6f);
            DrawParamField(state.Params, "Vig.smoothness", v => state.Params.Vig_smoothness = v, 0, 1);
            DrawParamField(state.Params, "Grain.intensity", v => state.Params.Grain_intensity = v, 0, 1);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(USD_Loc.T("aituning.runVariant") + " " + state.Name)) RunVariant(hub, state);
            if (GUILayout.Button(USD_Loc.C("ai.openFolder")) && !string.IsNullOrEmpty(state.Folder) && Directory.Exists(state.Folder))
            {
                EditorUtility.RevealInFinder(state.Folder);
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawParamField(USD_AiTuningParams p, string label, Action<float> setter, float min, float max)
        {
            var val = p.ToMap()[label];
            setter(EditorGUILayout.Slider(label, val, min, max));
        }

        private void RunVariant(USD_HubWindow hub, VariantState state)
        {
            if (_baseSnapshot == null || _baseMetrics == null || string.IsNullOrEmpty(_runRoot))
            {
                SetStatus(MessageType.Warning, USD_Loc.T("aituning.needBase"));
                return;
            }

            try
            {
                state.Folder = _runRoot + "/variant_" + state.Name;
                USD_EditorUtil.EnsureFolder(state.Folder);
                USD_EditorUtil.EnsureFolder(state.Folder + "/screenshots_after");

                var profilePath = state.Folder + "/profile.asset";
                CreateAndApplyProfile(profilePath, state.Params);

                var after = USD_AtmosScanner.CaptureSnapshot();
                File.WriteAllText(state.Folder + "/snapshot_after.json", JsonUtility.ToJson(after, true));

                var shotsAfter = USD_ScreenshotUtil.CaptureSixShots(state.Folder + "/screenshots_after", hub.ScreenshotWidth, hub.ScreenshotHeight, null);
                var metricsAfter = USD_VisionLiteUtil.BuildMetrics(hub.ActiveSceneName, USD_EditorUtil.Timestamp, _runRoot, shotsAfter);
                File.WriteAllText(state.Folder + "/image_metrics_after.json", JsonUtility.ToJson(metricsAfter, true));
                File.WriteAllText(state.Folder + "/image_metrics_diff.json", JsonUtility.ToJson(USD_VisionLiteUtil.BuildDiff(_baseMetrics, metricsAfter), true));

                var beforePath = _baseFolder + "/snapshot_before.json";
                var patch = USD_DeltaExtractor.Extract(hub.ActiveSceneName, beforePath, state.Folder + "/snapshot_after.json");
                if (patch != null) File.WriteAllText(state.Folder + "/deltaPatch.json", JsonUtility.ToJson(patch, true));

                AssetDatabase.Refresh();
                SetStatus(MessageType.Info, $"Variant {state.Name} completed: {state.Folder}");
            }
            catch (Exception e)
            {
                SetStatus(MessageType.Error, $"Run Variant {state.Name} failed: {e.Message}");
            }
        }

        private void DrawJudgePanel(USD_LabelCatalogAsset catalog)
        {
            if (string.IsNullOrEmpty(_runRoot)) return;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(USD_Loc.T("aituning.judge"), EditorStyles.boldLabel);

            var options = new[] { USD_Loc.T("ai.aBetter"), USD_Loc.T("ai.bBetter"), USD_Loc.T("ai.tie") };
            _pairwiseChoice = GUILayout.Toolbar(_pairwiseChoice, options);
            if (GUILayout.Button(USD_Loc.T("ai.savePairwise"))) SavePairwise();

            DrawAnnotationEditor(catalog, _variantA);
            DrawAnnotationEditor(catalog, _variantB);
        }

        private void DrawAnnotationEditor(USD_LabelCatalogAsset catalog, VariantState state)
        {
            EditorGUILayout.LabelField($"variant_{state.Name}/annotation.json", EditorStyles.miniBoldLabel);
            state.Score = EditorGUILayout.IntSlider(USD_Loc.T("ai.score"), state.Score, 1, 10);
            var issueIds = catalog.issues.Select(x => x.id).ToList();
            for (var i = 0; i < issueIds.Count; i++)
            {
                var selected = state.Tags.Contains(issueIds[i]);
                var next = EditorGUILayout.ToggleLeft(issueIds[i], selected);
                if (next && !selected) state.Tags.Add(issueIds[i]);
                if (!next && selected) state.Tags.Remove(issueIds[i]);
            }

            if (state.Tags.Count > 3) state.Tags = state.Tags.Take(3).ToList();
            if (GUILayout.Button(USD_Loc.T("ai.saveAnnotation") + $" ({state.Name})")) SaveVariantAnnotation(catalog, state);
        }

        private void SaveVariantAnnotation(USD_LabelCatalogAsset catalog, VariantState state)
        {
            if (string.IsNullOrEmpty(state.Folder))
            {
                SetStatus(MessageType.Warning, "Run variant first.");
                return;
            }

            var styleId = catalog.styles[Mathf.Clamp(_targetStyleIndex, 0, catalog.styles.Count - 1)].id;
            var annotation = new USD_Annotation
            {
                style_goal_id = styleId,
                score_1to10 = state.Score,
                issue_tags = state.Tags.Take(3).ToList(),
                source = "manual",
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            var path = state.Folder + "/annotation.json";
            File.WriteAllText(path, JsonUtility.ToJson(annotation, true));
            SetStatus(MessageType.Info, "Saved: " + path);
            AssetDatabase.Refresh();
        }

        private void SavePairwise()
        {
            if (string.IsNullOrEmpty(_runRoot)) return;
            var compareDir = _runRoot + "/compare";
            USD_EditorUtil.EnsureFolder(compareDir);
            var pref = new USD_PairwisePreference
            {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                styleA = "variant_A",
                styleB = "variant_B",
                choice = _pairwiseChoice == 0 ? "A better" : (_pairwiseChoice == 1 ? "B better" : "tie"),
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            File.WriteAllText(compareDir + "/pairwise_pref.json", JsonUtility.ToJson(pref, true));
            SetStatus(MessageType.Info, "Saved: " + compareDir + "/pairwise_pref.json");
            AssetDatabase.Refresh();
        }

        private void CreateAndApplyProfile(string profilePath, USD_AiTuningParams p)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);

            var bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = Mathf.Clamp(p.Bloom_intensity, 0, _bloomCeiling);
            bloom.scatter.overrideState = true;
            bloom.scatter.value = Mathf.Clamp01(p.Bloom_scatter);

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.overrideState = true;
            wb.temperature.value = Mathf.Clamp(p.WB_temperature, -20, 20);
            wb.tint.overrideState = true;
            wb.tint.value = Mathf.Clamp(p.WB_tint, -10, 10);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.overrideState = true;
            vig.intensity.value = Mathf.Clamp(p.Vig_intensity, 0, 0.6f);
            vig.smoothness.overrideState = true;
            vig.smoothness.value = Mathf.Clamp01(p.Vig_smoothness);

            var grain = profile.Add<FilmGrain>(true);
            grain.intensity.overrideState = true;
            grain.intensity.value = Mathf.Clamp01(p.Grain_intensity);

            var volume = GetOrCreateGlobalVolume();
            Undo.RecordObject(volume, "Assign AI Tuning Profile");
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
            AssetDatabase.SaveAssets();
        }

        private static Volume GetOrCreateGlobalVolume()
        {
            var volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            var global = volumes.FirstOrDefault(v => v != null && v.isGlobal);
            if (global != null) return global;

            var go = new GameObject("USD_GlobalVolume");
            Undo.RegisterCreatedObjectUndo(go, "Create USD_GlobalVolume");
            global = go.AddComponent<Volume>();
            global.isGlobal = true;
            return global;
        }

        private string BuildProposalPrompt(string styleId)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{\"target_style_goal_id\":\"" + styleId + "\",");
            sb.AppendLine("\"metrics_before\":" + JsonUtility.ToJson(_baseMetrics.aggregate) + ",");
            sb.AppendLine("\"metrics_goal\":{\"overexposure_ratio_lt\":" + _goalOverExposure.ToString("0.###") + ",\"center_contrast_ratio_gt\":" + _goalCenterContrast.ToString("0.###") + "},");
            sb.AppendLine("\"taste_policy\":{\"Vig.intensity\":0.2,\"Vig.smoothness\":0.2,\"Grain.intensity\":0.6,\"Bloom.scatter\":0.5,\"bloomCeiling\":" + _bloomCeiling.ToString("0.###") + "}}" );
            return sb.ToString();
        }

        private USD_AiTuningProposal BuildFallbackProposal(string styleId, USD_ImageMetricsAggregate m)
        {
            var baseBloom = m.brightness_bucket == "high" ? 0.1f : (m.brightness_bucket == "low" ? 2f : 1f);
            if (m.overexposure_ratio > 0.05f) baseBloom = 0.1f;

            var warmTemp = styleId == "warm" ? 10f : (styleId == "moody" ? -8f : 0f);
            var v = new USD_AiTuningProposal();
            v.variantA.@params.Bloom_intensity = baseBloom;
            v.variantA.@params.Bloom_scatter = 0.5f;
            v.variantA.@params.WB_temperature = warmTemp;
            v.variantA.@params.WB_tint = 0f;
            v.variantA.rationale = "基础稳健方案，控制过曝并保持统一氛围。";

            v.variantB.@params.Bloom_intensity = Mathf.Clamp(baseBloom * 0.6f, 0f, _bloomCeiling);
            v.variantB.@params.Bloom_scatter = 0.4f;
            v.variantB.@params.WB_temperature = warmTemp + (styleId == "warm" ? 4f : -4f);
            v.variantB.@params.WB_tint = styleId == "clean" ? -2f : 2f;
            v.variantB.rationale = "对比方案，提供更明显的冷暖差异用于 A/B 判断。";
            v.notes.Add("字段限制在白名单内并执行了 clamp。");
            return v;
        }

        private static USD_AiTuningProposal TryParseProposal(string text)
        {
            var json = ExtractJsonObject(text);
            if (string.IsNullOrEmpty(json)) return null;
            var s = json.Replace("Bloom.intensity", "Bloom_intensity")
                .Replace("Bloom.scatter", "Bloom_scatter")
                .Replace("WB.temperature", "WB_temperature")
                .Replace("WB.tint", "WB_tint")
                .Replace("Vig.intensity", "Vig_intensity")
                .Replace("Vig.smoothness", "Vig_smoothness")
                .Replace("Grain.intensity", "Grain_intensity");
            try { return JsonUtility.FromJson<USD_AiTuningProposal>(s); }
            catch { return null; }
        }

        private static string ExtractJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            return start >= 0 && end > start ? text.Substring(start, end - start + 1) : string.Empty;
        }

        private string ToProposalJson(USD_AiTuningProposal p)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            AppendVariant(sb, "variantA", p.variantA);
            sb.AppendLine(",");
            AppendVariant(sb, "variantB", p.variantB);
            sb.AppendLine(",");
            sb.AppendLine("  \"notes\": [" + string.Join(",", p.notes.Select(n => "\"" + n.Replace("\"", "'") + "\"")) + "]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void AppendVariant(StringBuilder sb, string name, USD_AiTuningProposalVariant v)
        {
            var m = v.@params.ToMap();
            sb.AppendLine($"  \"{name}\": {{");
            sb.AppendLine("    \"params\": {");
            sb.AppendLine($"      \"Bloom.intensity\": {m["Bloom.intensity"]:0.###},");
            sb.AppendLine($"      \"Bloom.scatter\": {m["Bloom.scatter"]:0.###},");
            sb.AppendLine($"      \"WB.temperature\": {m["WB.temperature"]:0.###},");
            sb.AppendLine($"      \"WB.tint\": {m["WB.tint"]:0.###},");
            sb.AppendLine($"      \"Vig.intensity\": {m["Vig.intensity"]:0.###},");
            sb.AppendLine($"      \"Vig.smoothness\": {m["Vig.smoothness"]:0.###},");
            sb.AppendLine($"      \"Grain.intensity\": {m["Grain.intensity"]:0.###}");
            sb.AppendLine("    },");
            sb.AppendLine("    \"rationale\": \"" + (v.rationale ?? string.Empty).Replace("\"", "'") + "\"");
            sb.Append("  }");
        }

        private void ClampAndNormalize(USD_AiTuningParams p, USD_ImageMetricsAggregate metrics)
        {
            p.Vig_intensity = 0.2f;
            p.Vig_smoothness = 0.2f;
            p.Grain_intensity = 0.6f;
            p.Bloom_scatter = 0.5f;
            p.Bloom_intensity = Mathf.Clamp(p.Bloom_intensity, 0f, _bloomCeiling);
            if (metrics.overexposure_ratio > 0.05f) p.Bloom_intensity = Mathf.Min(p.Bloom_intensity, 0.3f);
            p.WB_temperature = Mathf.Clamp(p.WB_temperature, -20f, 20f);
            p.WB_tint = Mathf.Clamp(p.WB_tint, -10f, 10f);
        }

        private static void EnsureVariantDifference(USD_AiTuningProposal p)
        {
            var a = p.variantA.@params;
            var b = p.variantB.@params;
            var diffCount = 0;
            foreach (var key in a.ToMap().Keys)
            {
                if (Mathf.Abs(a.ToMap()[key] - b.ToMap()[key]) > 0.001f) diffCount++;
            }
            if (diffCount >= 2) return;
            b.WB_temperature = Mathf.Clamp(b.WB_temperature + 4f, -20f, 20f);
            b.Bloom_intensity = Mathf.Max(0f, b.Bloom_intensity * 0.7f);
        }

        private static string GetStyleDisplay(USD_StyleGoal x)
        {
            return USD_Loc.CurrentLang() == "zh" ? $"{x.name_zh} ({x.id})" : $"{x.name_en} ({x.id})";
        }

        private void SetStatus(MessageType type, string text)
        {
            _statusType = type;
            _status = text;
            Debug.Log("[USD][AITuning] " + text);
        }
    }
}
