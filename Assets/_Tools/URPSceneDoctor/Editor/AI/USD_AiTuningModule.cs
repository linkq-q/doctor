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
    [Serializable] internal sealed class USD_SmhHint { public float shadowsBias; public float highlightsBias; }

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
        public float CA_postExposure;
        public float CA_contrast;
        public float CA_saturation;

        public Dictionary<string, float> ToMap() => new Dictionary<string, float>
        {
            {"Bloom.intensity", Bloom_intensity}, {"Bloom.scatter", Bloom_scatter}, {"WB.temperature", WB_temperature}, {"WB.tint", WB_tint},
            {"Vig.intensity", Vig_intensity}, {"Vig.smoothness", Vig_smoothness}, {"Grain.intensity", Grain_intensity},
            {"CA.postExposure", CA_postExposure}, {"CA.contrast", CA_contrast}, {"CA.saturation", CA_saturation}
        };

        public void FromMap(Dictionary<string, float> map)
        {
            Bloom_intensity = Get(map, "Bloom.intensity", 0f); Bloom_scatter = Get(map, "Bloom.scatter", 0.5f);
            WB_temperature = Get(map, "WB.temperature", 0f); WB_tint = Get(map, "WB.tint", 0f);
            Vig_intensity = Get(map, "Vig.intensity", 0.2f); Vig_smoothness = Get(map, "Vig.smoothness", 0.2f);
            Grain_intensity = Get(map, "Grain.intensity", 0.6f);
            CA_postExposure = Get(map, "CA.postExposure", 0f); CA_contrast = Get(map, "CA.contrast", 0f); CA_saturation = Get(map, "CA.saturation", 0f);
        }

        private static float Get(Dictionary<string, float> map, string key, float fallback) => map != null && map.TryGetValue(key, out var v) ? v : fallback;
    }

    [Serializable] internal sealed class USD_AiTuningVariant { public USD_AiTuningParams @params = new USD_AiTuningParams(); public string curve_preset = "S_Curve_Soft"; public USD_SmhHint smh_hint = new USD_SmhHint(); public string rationale; }
    [Serializable] internal sealed class USD_AiTuningProposal { public USD_AiTuningVariant variantA = new USD_AiTuningVariant(); public USD_AiTuningVariant variantB = new USD_AiTuningVariant(); public List<string> notes = new List<string>(); public string forceDiversityStatus = "Unknown"; public List<string> warnings = new List<string>(); }

    public sealed class USD_AiTuningModule : IToolModule
    {
        public string ModuleName => "AI Tuning";

        private readonly VariantState _variantA = new VariantState("A");
        private readonly VariantState _variantB = new VariantState("B");
        private string _runRoot, _baseFolder, _status, _baseHueBias = "purple", _contrastPairHint = "warm", _forceStatus = "Unknown";
        private MessageType _statusType = MessageType.Info;
        private int _targetStyleIndex, _pairwiseChoice;
        private bool _useMetricsGoal = true;
        private float _goalOverExposure = 0.01f, _goalCenterContrast = 1.1f, _bloomCeiling = 5f;
        private USD_ImageMetricsFile _baseMetrics;
        private USD_ScanSnapshot _baseSnapshot;
        private USD_AiTuningProposal _proposal;
        private readonly USD_LlmCallStatus _llmStatus = new USD_LlmCallStatus();

        private sealed class VariantState { public readonly string Name; public readonly USD_AiTuningParams Params = new USD_AiTuningParams(); public string CurvePreset = "S_Curve_Soft"; public USD_SmhHint Smh = new USD_SmhHint(); public int Score = 5; public List<string> Tags = new List<string>(); public string Folder; public VariantState(string n){Name=n;} }

        public void DrawUI(USD_HubWindow hub)
        {
            var settings = USD_Settings.GetOrCreateSettings();
            var catalog = settings.labelCatalog != null ? settings.labelCatalog : USD_LabelCatalogUtil.GetOrCreateDefault();
            EditorGUILayout.HelpBox(USD_Loc.T("aituning.overview"), MessageType.Info);
            if (catalog == null || catalog.styles == null || catalog.styles.Count == 0) { EditorGUILayout.HelpBox(USD_Loc.T("ai.errNoCatalog"), MessageType.Error); return; }

            _targetStyleIndex = EditorGUILayout.Popup(USD_Loc.T("aituning.targetStyle"), Mathf.Clamp(_targetStyleIndex,0,catalog.styles.Count-1), catalog.styles.Select(GetStyleDisplay).ToArray());
            _baseHueBias = EditorGUILayout.TextField("baseHueBias", _baseHueBias);
            _contrastPairHint = EditorGUILayout.TextField("contrastPairHint", _contrastPairHint);
            _useMetricsGoal = EditorGUILayout.ToggleLeft(USD_Loc.T("aituning.useMetricsGoal"), _useMetricsGoal);
            if (_useMetricsGoal) { _goalOverExposure = EditorGUILayout.Slider("overexposure_ratio <", _goalOverExposure, 0f, 0.2f); _goalCenterContrast = EditorGUILayout.Slider("center_contrast_ratio >", _goalCenterContrast, 0.8f, 1.5f); }
            _bloomCeiling = EditorGUILayout.Slider("bloomCeiling", _bloomCeiling, 1f, 10f);
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, _statusType);
            DrawLlmStatus();
            EditorGUILayout.HelpBox("Force Diversity: " + _forceStatus, _forceStatus == "Failed" ? MessageType.Warning : MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(USD_Loc.C("aituning.captureBase"))) CaptureBase(hub);
            if (GUILayout.Button(USD_Loc.C("aituning.propose"))) Propose(settings, catalog);
            if (GUILayout.Button(USD_Loc.C("aituning.runBoth"))) { RunVariant(hub, _variantA); RunVariant(hub, _variantB); CheckVisualDifference(); }
            EditorGUILayout.EndHorizontal();

            DrawVariantCard(hub, _variantA, _proposal?.variantA?.rationale);
            DrawVariantCard(hub, _variantB, _proposal?.variantB?.rationale);
            DrawJudgePanel(catalog);

            if (!string.IsNullOrEmpty(_runRoot)) { EditorGUILayout.BeginHorizontal(); if (GUILayout.Button(USD_Loc.C("ai.openFolder"))) EditorUtility.RevealInFinder(_runRoot); EditorGUILayout.LabelField(_runRoot, EditorStyles.miniLabel); EditorGUILayout.EndHorizontal(); }
        }

        public USD_ModuleResult Execute(USD_RunContext context) => new USD_ModuleResult { ModuleName = ModuleName, Snapshot = context.Snapshot ?? USD_AtmosScanner.CaptureSnapshot() };

        private void CaptureBase(USD_HubWindow hub)
        {
            try
            {
                _runRoot = $"Assets/_Tools/URPSceneDoctor/AITuningRuns/{DateTime.Now:yyyyMMdd_HHmmss}";
                _baseFolder = _runRoot + "/base";
                USD_EditorUtil.EnsureFolder("Assets/_Tools/URPSceneDoctor/AITuningRuns"); USD_EditorUtil.EnsureFolder(_runRoot); USD_EditorUtil.EnsureFolder(_baseFolder); USD_EditorUtil.EnsureFolder(_baseFolder + "/screenshots_before");
                _baseSnapshot = USD_AtmosScanner.CaptureSnapshot(); File.WriteAllText(_baseFolder + "/snapshot_before.json", JsonUtility.ToJson(_baseSnapshot, true));
                var shots = USD_ScreenshotUtil.CaptureSixShots(_baseFolder + "/screenshots_before", hub.ScreenshotWidth, hub.ScreenshotHeight, null);
                _baseMetrics = USD_VisionLiteUtil.BuildMetrics(hub.ActiveSceneName, USD_EditorUtil.Timestamp, _runRoot, shots);
                File.WriteAllText(_baseFolder + "/image_metrics_before.json", JsonUtility.ToJson(_baseMetrics, true));
                AssetDatabase.Refresh(); SetStatus(MessageType.Info, "Base captured: " + _baseFolder);
            }
            catch (Exception e) { SetStatus(MessageType.Error, "Capture Base failed: " + e.Message); }
        }

        private void Propose(USD_Settings settings, USD_LabelCatalogAsset catalog)
        {
            if (_baseSnapshot == null || _baseMetrics == null) { SetStatus(MessageType.Warning, USD_Loc.T("aituning.needBase")); return; }
            var styleId = catalog.styles[Mathf.Clamp(_targetStyleIndex,0,catalog.styles.Count-1)].id;
            _proposal = BuildFallbackProposal(styleId, _baseMetrics.aggregate);
            if (USD_LlmClient.IsEnabled(settings))
            {
                _llmStatus.MarkSending("已发送请求（AI Tuning）...");
                _llmStatus.MarkWaiting("正在等待 AI 回复...");
                var res = USD_LlmClient.Chat(settings, BuildSystemPrompt(), BuildProposalPrompt(styleId));
                if (res.success)
                {
                    var parsed = TryParseProposal(res.text);
                    if (parsed != null)
                    {
                        _proposal = parsed;
                        _llmStatus.MarkSuccess("AI 回复成功：已生成 A/B 方案。");
                    }
                    else
                    {
                        _proposal.warnings.Add("ParseFailed: fallback used");
                        _llmStatus.MarkFail("返回解析失败，已使用兜底方案。");
                    }
                }
                else
                {
                    _proposal.warnings.Add("LLMFailed:" + res.error);
                    _llmStatus.MarkFail(string.IsNullOrEmpty(res.error) ? "未知错误" : res.error);
                }
            }

            NormalizeByPolicy(_proposal.variantA, _baseMetrics.aggregate); NormalizeByPolicy(_proposal.variantB, _baseMetrics.aggregate);
            var force = EnforceForceDiversity(settings, styleId);
            _forceStatus = force;
            _proposal.forceDiversityStatus = force;
            _variantA.Params.FromMap(_proposal.variantA.@params.ToMap()); _variantA.CurvePreset = _proposal.variantA.curve_preset; _variantA.Smh = _proposal.variantA.smh_hint;
            _variantB.Params.FromMap(_proposal.variantB.@params.ToMap()); _variantB.CurvePreset = _proposal.variantB.curve_preset; _variantB.Smh = _proposal.variantB.smh_hint;

            var proposalDir = _runRoot + "/proposal"; USD_EditorUtil.EnsureFolder(proposalDir);
            File.WriteAllText(proposalDir + "/ai_param_proposal.json", ToProposalJson(_proposal)); AssetDatabase.Refresh();
            SetStatus(MessageType.Info, "Proposal generated: " + proposalDir + "/ai_param_proposal.json");
        }

        private string EnforceForceDiversity(USD_Settings settings, string styleId)
        {
            if (DiversityOk(_proposal.variantA.@params, _proposal.variantB.@params)) return "OK";
            if (USD_LlmClient.IsEnabled(settings))
            {
                var oldTemp = settings.llmTemperature; settings.llmTemperature = 0.8f;
                _llmStatus.MarkSending("已发送重试请求（Force Diversity）...");
                _llmStatus.MarkWaiting("正在等待重试回复...");
                var retry = USD_LlmClient.Chat(settings, BuildSystemPrompt(), BuildProposalPrompt(styleId));
                settings.llmTemperature = oldTemp;
                if (retry.success)
                {
                    var parsed = TryParseProposal(retry.text);
                    if (parsed != null)
                    {
                        _proposal.variantA = parsed.variantA; _proposal.variantB = parsed.variantB; _proposal.notes.AddRange(parsed.notes);
                        NormalizeByPolicy(_proposal.variantA, _baseMetrics.aggregate); NormalizeByPolicy(_proposal.variantB, _baseMetrics.aggregate);
                        if (DiversityOk(_proposal.variantA.@params, _proposal.variantB.@params))
                        {
                            _llmStatus.MarkSuccess("Force Diversity 重试成功。");
                            return "OK";
                        }
                    }
                }
                else
                {
                    _llmStatus.MarkFail(string.IsNullOrEmpty(retry.error) ? "重试失败" : retry.error);
                }
            }

            var map = _proposal.variantB.@params.ToMap();
            map["WB.temperature"] = Mathf.Clamp(map["WB.temperature"] + (_contrastPairHint == "warm" ? 8f : -8f), -20f, 20f);
            map["CA.contrast"] = Mathf.Clamp(map["CA.contrast"] + 6f, -50f, 50f);
            map["CA.postExposure"] = Mathf.Clamp(map["CA.postExposure"] + (_baseMetrics.aggregate.overexposure_ratio >= 0.01f ? -0.3f : 0.3f), -2f, 2f);
            _proposal.variantB.@params.FromMap(map);
            _proposal.warnings.Add("ForceDiversityApplied: programmatically widened variantB");
            return DiversityOk(_proposal.variantA.@params, _proposal.variantB.@params) ? "Applied" : "Failed";
        }

        private static bool DiversityOk(USD_AiTuningParams a, USD_AiTuningParams b)
        {
            var am = a.ToMap(); var bm = b.ToMap();
            var differentFields = am.Count(kv => Mathf.Abs(kv.Value - bm[kv.Key]) > 0.001f);
            var rules = 0;
            if (Mathf.Abs(am["WB.temperature"] - bm["WB.temperature"]) >= 8f) rules++;
            if (Mathf.Abs(am["CA.contrast"] - bm["CA.contrast"]) >= 6f) rules++;
            if (Mathf.Abs(am["CA.postExposure"] - bm["CA.postExposure"]) >= 0.3f) rules++;
            if (Mathf.Abs(am["Bloom.intensity"] - bm["Bloom.intensity"]) >= 0.5f) rules++;
            return differentFields >= 3 && rules >= 2;
        }

        private void DrawVariantCard(USD_HubWindow hub, VariantState state, string rationale)
        {
            EditorGUILayout.Space(6); EditorGUILayout.LabelField("Variant " + state.Name, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(rationale)) EditorGUILayout.HelpBox(rationale, MessageType.None);
            DrawSlider("Bloom.intensity", ref state.Params.Bloom_intensity, 0f, _bloomCeiling); DrawSlider("Bloom.scatter", ref state.Params.Bloom_scatter, 0f, 1f);
            DrawSlider("WB.temperature", ref state.Params.WB_temperature, -20f, 20f); DrawSlider("WB.tint", ref state.Params.WB_tint, -10f, 10f);
            DrawSlider("Vig.intensity", ref state.Params.Vig_intensity, 0f, 0.6f); DrawSlider("Vig.smoothness", ref state.Params.Vig_smoothness, 0f, 1f);
            DrawSlider("Grain.intensity", ref state.Params.Grain_intensity, 0f, 1f); DrawSlider("CA.postExposure", ref state.Params.CA_postExposure, -2f, 2f);
            DrawSlider("CA.contrast", ref state.Params.CA_contrast, -50f, 50f); DrawSlider("CA.saturation", ref state.Params.CA_saturation, -100f, 100f);
            state.CurvePreset = EditorGUILayout.Popup("curve_preset", state.CurvePreset == "S_Curve_Strong" ? 1 : 0, new[] { "S_Curve_Soft", "S_Curve_Strong" }) == 1 ? "S_Curve_Strong" : "S_Curve_Soft";
            state.Smh.shadowsBias = EditorGUILayout.Slider("smh.shadowsBias", state.Smh.shadowsBias, -0.1f, 0.1f);
            state.Smh.highlightsBias = EditorGUILayout.Slider("smh.highlightsBias", state.Smh.highlightsBias, -0.1f, 0.1f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(USD_Loc.T("aituning.runVariant") + " " + state.Name)) RunVariant(hub, state);
            if (GUILayout.Button(USD_Loc.C("ai.openFolder")) && !string.IsNullOrEmpty(state.Folder) && Directory.Exists(state.Folder)) EditorUtility.RevealInFinder(state.Folder);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawSlider(string label, ref float value, float min, float max) => value = EditorGUILayout.Slider(label, value, min, max);

        private void RunVariant(USD_HubWindow hub, VariantState state)
        {
            if (_baseSnapshot == null || _baseMetrics == null || string.IsNullOrEmpty(_runRoot)) { SetStatus(MessageType.Warning, USD_Loc.T("aituning.needBase")); return; }
            try
            {
                state.Folder = _runRoot + "/variant_" + state.Name; USD_EditorUtil.EnsureFolder(state.Folder); USD_EditorUtil.EnsureFolder(state.Folder + "/screenshots_after");
                CreateAndApplyProfile(state.Folder + "/profile.asset", state.Params, state.CurvePreset, state.Smh);

                var after = USD_AtmosScanner.CaptureSnapshot();
                after.curvePreset = state.CurvePreset; after.smhShadowsBias = state.Smh.shadowsBias; after.smhHighlightsBias = state.Smh.highlightsBias;
                File.WriteAllText(state.Folder + "/snapshot_after.json", JsonUtility.ToJson(after, true));
                var shots = USD_ScreenshotUtil.CaptureSixShots(state.Folder + "/screenshots_after", hub.ScreenshotWidth, hub.ScreenshotHeight, null);
                var mAfter = USD_VisionLiteUtil.BuildMetrics(hub.ActiveSceneName, USD_EditorUtil.Timestamp, _runRoot, shots);
                File.WriteAllText(state.Folder + "/image_metrics_after.json", JsonUtility.ToJson(mAfter, true));
                File.WriteAllText(state.Folder + "/image_metrics_diff.json", JsonUtility.ToJson(USD_VisionLiteUtil.BuildDiff(_baseMetrics, mAfter), true));
                var patch = USD_DeltaExtractor.Extract(hub.ActiveSceneName, _baseFolder + "/snapshot_before.json", state.Folder + "/snapshot_after.json");
                if (patch != null) File.WriteAllText(state.Folder + "/deltaPatch.json", JsonUtility.ToJson(patch, true));
                AssetDatabase.Refresh(); SetStatus(MessageType.Info, $"Variant {state.Name} completed: {state.Folder}");
            }
            catch (Exception e) { SetStatus(MessageType.Error, $"Run Variant {state.Name} failed: {e.Message}"); }
        }

        private void DrawJudgePanel(USD_LabelCatalogAsset catalog)
        {
            if (string.IsNullOrEmpty(_runRoot)) return;
            EditorGUILayout.Space(8); EditorGUILayout.LabelField(USD_Loc.T("aituning.judge"), EditorStyles.boldLabel);
            _pairwiseChoice = GUILayout.Toolbar(_pairwiseChoice, new[] { USD_Loc.T("ai.aBetter"), USD_Loc.T("ai.bBetter"), USD_Loc.T("ai.tie") });
            if (GUILayout.Button(USD_Loc.T("ai.savePairwise"))) SavePairwise();
            DrawAnnotationEditor(catalog, _variantA); DrawAnnotationEditor(catalog, _variantB);
        }

        private void DrawAnnotationEditor(USD_LabelCatalogAsset catalog, VariantState state)
        {
            EditorGUILayout.LabelField($"variant_{state.Name}/annotation.json", EditorStyles.miniBoldLabel);
            state.Score = EditorGUILayout.IntSlider(USD_Loc.T("ai.score"), state.Score, 1, 10);
            foreach (var id in catalog.issues.Select(x => x.id).ToList())
            {
                var has = state.Tags.Contains(id); var next = EditorGUILayout.ToggleLeft(id, has);
                if (next && !has) state.Tags.Add(id); if (!next && has) state.Tags.Remove(id);
            }
            if (state.Tags.Count > 3) state.Tags = state.Tags.Take(3).ToList();
            if (GUILayout.Button(USD_Loc.T("ai.saveAnnotation") + $" ({state.Name})")) SaveVariantAnnotation(catalog, state);
        }

        private void SaveVariantAnnotation(USD_LabelCatalogAsset catalog, VariantState state)
        {
            if (string.IsNullOrEmpty(state.Folder)) { SetStatus(MessageType.Warning, "Run variant first."); return; }
            var a = new USD_Annotation { style_goal_id = catalog.styles[Mathf.Clamp(_targetStyleIndex,0,catalog.styles.Count-1)].id, score_1to10 = state.Score, issue_tags = state.Tags.Take(3).ToList(), source = "manual", timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            File.WriteAllText(state.Folder + "/annotation.json", JsonUtility.ToJson(a, true)); AssetDatabase.Refresh(); SetStatus(MessageType.Info, "Saved: " + state.Folder + "/annotation.json");
        }

        private void SavePairwise()
        {
            if (string.IsNullOrEmpty(_runRoot)) return;
            var compare = _runRoot + "/compare"; USD_EditorUtil.EnsureFolder(compare);
            var p = new USD_PairwisePreference { scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, styleA = "variant_A", styleB = "variant_B", choice = _pairwiseChoice == 0 ? "A better" : (_pairwiseChoice == 1 ? "B better" : "tie"), timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            File.WriteAllText(compare + "/pairwise_pref.json", JsonUtility.ToJson(p, true)); AssetDatabase.Refresh(); SetStatus(MessageType.Info, "Saved: " + compare + "/pairwise_pref.json");
        }

        private void CreateAndApplyProfile(string path, USD_AiTuningParams p, string curvePreset, USD_SmhHint smh)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path);
            var bloom = profile.Add<Bloom>(true); bloom.active = true; bloom.intensity.overrideState = true; bloom.intensity.value = Mathf.Clamp(p.Bloom_intensity, 0f, _bloomCeiling); bloom.scatter.overrideState = true; bloom.scatter.value = 0.5f;
            var vig = profile.Add<Vignette>(true); vig.intensity.overrideState = true; vig.intensity.value = 0.2f; vig.smoothness.overrideState = true; vig.smoothness.value = 0.2f;
            var grain = profile.Add<FilmGrain>(true); grain.type.overrideState = true; grain.type.value = FilmGrainLookup.Thin1; grain.intensity.overrideState = true; grain.intensity.value = 0.6f;
            var wb = profile.Add<WhiteBalance>(true); wb.temperature.overrideState = true; wb.temperature.value = Mathf.Clamp(p.WB_temperature,-20f,20f); wb.tint.overrideState = true; wb.tint.value = Mathf.Clamp(p.WB_tint,-10f,10f);
            var ca = profile.Add<ColorAdjustments>(true); ca.postExposure.overrideState = true; ca.postExposure.value = Mathf.Clamp(p.CA_postExposure,-2f,2f); ca.contrast.overrideState = true; ca.contrast.value = Mathf.Clamp(p.CA_contrast,-50f,50f); ca.saturation.overrideState = true; ca.saturation.value = Mathf.Clamp(p.CA_saturation,-100f,100f);
            var curves = profile.Add<ColorCurves>(true); curves.master.overrideState = true; curves.master.value = curvePreset == "S_Curve_Strong" ? BuildStrongCurve() : BuildSoftCurve();
            var smhComp = profile.Add<ShadowsMidtonesHighlights>(true);
            smhComp.shadows.overrideState = true; smhComp.midtones.overrideState = true; smhComp.highlights.overrideState = true;
            var sign = _contrastPairHint == "warm" ? 1f : -1f;
            var settings = USD_Settings.GetOrCreateSettings();
            var maxOffset = settings != null ? Mathf.Max(0.01f, settings.smhMaxOffset) : 0.1f;
            var shadowV = ClampSvhVector(new Vector3(smh.shadowsBias * sign, smh.shadowsBias * -sign, smh.shadowsBias * 0.4f), maxOffset);
            var midV = ClampSvhVector(new Vector3(smh.shadowsBias * 0.5f * sign, smh.shadowsBias * -0.5f * sign, smh.shadowsBias * 0.2f), maxOffset * 0.5f);
            var highV = ClampSvhVector(new Vector3(smh.highlightsBias * -sign, smh.highlightsBias * sign, smh.highlightsBias * 0.4f), maxOffset);
            smhComp.shadows.value = new Vector4(shadowV.x, shadowV.y, shadowV.z, 0f);
            smhComp.midtones.value = new Vector4(midV.x, midV.y, midV.z, 0f);
            smhComp.highlights.value = new Vector4(highV.x, highV.y, highV.z, 0f);
            smh.shadowsBias = shadowV.magnitude;
            smh.highlightsBias = highV.magnitude;

            var volume = GetOrCreateGlobalVolume(); Undo.RecordObject(volume, "Assign AI Tuning Profile"); volume.sharedProfile = profile; EditorUtility.SetDirty(volume); AssetDatabase.SaveAssets();
        }

        private static TextureCurve BuildSoftCurve() => new TextureCurve(new AnimationCurve(new Keyframe(0f,0f), new Keyframe(0.35f,0.3f), new Keyframe(0.7f,0.78f), new Keyframe(1f,1f)), 0f, false, new Vector2(0f,1f));
        private static TextureCurve BuildStrongCurve() => new TextureCurve(new AnimationCurve(new Keyframe(0f,0f), new Keyframe(0.25f,0.15f), new Keyframe(0.75f,0.88f), new Keyframe(1f,1f)), 0f, false, new Vector2(0f,1f));

        private static Volume GetOrCreateGlobalVolume()
        {
            var volume = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).FirstOrDefault(v => v != null && v.isGlobal);
            if (volume != null) return volume;
            var go = new GameObject("USD_GlobalVolume"); Undo.RegisterCreatedObjectUndo(go, "Create USD_GlobalVolume"); volume = go.AddComponent<Volume>(); volume.isGlobal = true; return volume;
        }

        private void NormalizeByPolicy(USD_AiTuningVariant v, USD_ImageMetricsAggregate m)
        {
            v.@params.Vig_intensity = 0.2f; v.@params.Vig_smoothness = 0.2f; v.@params.Grain_intensity = 0.6f; v.@params.Bloom_scatter = 0.5f;
            var bucketBase = m.brightness_bucket == "high" ? 0.1f : (m.brightness_bucket == "low" ? 10f : 1f);
            v.@params.Bloom_intensity = Mathf.Clamp(v.@params.Bloom_intensity <= 0 ? bucketBase : v.@params.Bloom_intensity, 0f, _bloomCeiling);
            if (m.overexposure_ratio >= 0.01f) { v.@params.Bloom_intensity = Mathf.Min(v.@params.Bloom_intensity, 0.3f); v.@params.CA_postExposure = Mathf.Min(v.@params.CA_postExposure, 0f);  }
            if (m.luma_std < 0.12f || m.center_contrast_ratio < 1.1f) { if (v.curve_preset == "S_Curve_Soft") v.curve_preset = "S_Curve_Strong"; v.smh_hint.shadowsBias = Mathf.Max(v.smh_hint.shadowsBias, 0.04f); }
            v.@params.WB_temperature = Mathf.Clamp(v.@params.WB_temperature, -20f, 20f); v.@params.WB_tint = Mathf.Clamp(v.@params.WB_tint, -10f, 10f);
            v.@params.CA_postExposure = Mathf.Clamp(v.@params.CA_postExposure, -2f, 2f); v.@params.CA_contrast = Mathf.Clamp(v.@params.CA_contrast, -50f, 50f); v.@params.CA_saturation = Mathf.Clamp(v.@params.CA_saturation, -100f, 100f);
            v.smh_hint.shadowsBias = Mathf.Clamp(v.smh_hint.shadowsBias, -0.1f, 0.1f); v.smh_hint.highlightsBias = Mathf.Clamp(v.smh_hint.highlightsBias, -0.1f, 0.1f);
        }

        private string BuildSystemPrompt() => "你是URP 场景氛围调参助手。严格输出 JSON。只能输出白名单字段。必须输出A=保守干净、B=大胆氛围；暗角0.2/0.2、grain thin1+0.6、bloom scatter 0.5，低对比优先曲线/SMH。";

        private string BuildProposalPrompt(string styleId)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("\"target_style_goal_id\":\"" + styleId + "\",");
            sb.AppendLine("\"baseHueBias\":\"" + _baseHueBias + "\",");
            sb.AppendLine("\"contrastPairHint\":\"" + _contrastPairHint + "\",");
            sb.AppendLine("\"taste_policy\":{\"vig\":\"0.2/0.2\",\"grain\":\"Thin1+0.6\",\"bloomScatter\":0.5,\"bloomCeiling\":" + _bloomCeiling.ToString("0.###") + "},");
            sb.AppendLine("\"metrics_before\":" + JsonUtility.ToJson(_baseMetrics.aggregate) + ",");
            sb.AppendLine("\"metrics_goal\":{\"overexposure_ratio_lt\":0.01,\"center_contrast_ratio_gt\":1.1,\"brightness_bucket\":\"mid\"},");
            sb.AppendLine("\"snapshot_before_volumeKeyValues\":" + ToJsonVolumeMap(_baseSnapshot.volumeKeyValues));
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string ToJsonVolumeMap(Dictionary<string,float> map)
        {
            var parts = map.OrderBy(x=>x.Key).Select(kv => "\"" + kv.Key + "\":" + kv.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            return "{" + string.Join(",", parts) + "}";
        }

        private USD_AiTuningProposal BuildFallbackProposal(string styleId, USD_ImageMetricsAggregate m)
        {
            var p = new USD_AiTuningProposal();
            var baseBloom = m.brightness_bucket == "high" ? 0.1f : (m.brightness_bucket == "low" ? 10f : 1f);
            p.variantA.@params.Bloom_intensity = Mathf.Clamp(baseBloom, 0f, _bloomCeiling); p.variantA.@params.WB_temperature = styleId == "warm" ? 6f : (styleId == "moody" ? -6f : 0f); p.variantA.@params.CA_contrast = 4f; p.variantA.curve_preset = "S_Curve_Soft"; p.variantA.smh_hint.shadowsBias = 0.03f; p.variantA.rationale = "保守提案，控制风险并保持干净。";
            p.variantB.@params.Bloom_intensity = Mathf.Clamp(baseBloom * 0.6f, 0f, _bloomCeiling); p.variantB.@params.WB_temperature = p.variantA.@params.WB_temperature + (styleId == "warm" ? 10f : -10f); p.variantB.@params.CA_postExposure = -0.3f; p.variantB.@params.CA_contrast = 12f; p.variantB.@params.CA_saturation = 10f; p.variantB.curve_preset = "S_Curve_Strong"; p.variantB.smh_hint.shadowsBias = 0.07f; p.variantB.smh_hint.highlightsBias = 0.05f; p.variantB.rationale = "大胆提案，强化氛围与冷暖差。";
            p.notes.Add("A/B 按方法论强制分化。");
            return p;
        }

        private static USD_AiTuningProposal TryParseProposal(string text)
        {
            var json = ExtractJson(text); if (string.IsNullOrEmpty(json)) return null;
            var s = json.Replace("Bloom.intensity","Bloom_intensity").Replace("Bloom.scatter","Bloom_scatter").Replace("WB.temperature","WB_temperature").Replace("WB.tint","WB_tint").Replace("Vig.intensity","Vig_intensity").Replace("Vig.smoothness","Vig_smoothness").Replace("Grain.intensity","Grain_intensity").Replace("CA.postExposure","CA_postExposure").Replace("CA.contrast","CA_contrast").Replace("CA.saturation","CA_saturation").Replace("shadowsBias","shadowsBias").Replace("highlightsBias","highlightsBias");
            try { return JsonUtility.FromJson<USD_AiTuningProposal>(s); } catch { return null; }
        }

        private static string ExtractJson(string text) { if (string.IsNullOrWhiteSpace(text)) return string.Empty; var a=text.IndexOf('{'); var b=text.LastIndexOf('}'); return a>=0&&b>a?text.Substring(a,b-a+1):string.Empty; }

        private string ToProposalJson(USD_AiTuningProposal p)
        {
            string JV(USD_AiTuningVariant v)
            {
                var m=v.@params.ToMap();
                return "{\n" +
                    "  \"params\": {\n" +
                    $"    \"Bloom.intensity\": {m["Bloom.intensity"]:0.###},\n    \"Bloom.scatter\": {m["Bloom.scatter"]:0.###},\n    \"WB.temperature\": {m["WB.temperature"]:0.###},\n    \"WB.tint\": {m["WB.tint"]:0.###},\n    \"Vig.intensity\": {m["Vig.intensity"]:0.###},\n    \"Vig.smoothness\": {m["Vig.smoothness"]:0.###},\n    \"Grain.intensity\": {m["Grain.intensity"]:0.###},\n    \"CA.postExposure\": {m["CA.postExposure"]:0.###},\n    \"CA.contrast\": {m["CA.contrast"]:0.###},\n    \"CA.saturation\": {m["CA.saturation"]:0.###}\n" +
                    "  },\n" +
                    $"  \"curve_preset\": \"{v.curve_preset}\",\n  \"smh_hint\": {{ \"shadowsBias\": {v.smh_hint.shadowsBias:0.###}, \"highlightsBias\": {v.smh_hint.highlightsBias:0.###} }},\n" +
                    $"  \"rationale\": \"{(v.rationale??string.Empty).Replace("\"","'")}\"\n" +
                    "}";
            }
            return "{\n"+
                "\"variantA\": "+JV(p.variantA)+",\n"+
                "\"variantB\": "+JV(p.variantB)+",\n"+
                "\"notes\": ["+string.Join(",", p.notes.Select(n=>"\""+n.Replace("\"","'")+"\""))+"],\n"+
                "\"forceDiversityStatus\": \""+p.forceDiversityStatus+"\",\n"+
                "\"warnings\": ["+string.Join(",", p.warnings.Select(n=>"\""+n.Replace("\"","'")+"\""))+"]\n"+
                "}";
        }

        private void CheckVisualDifference()
        {
            if (string.IsNullOrEmpty(_variantA.Folder) || string.IsNullOrEmpty(_variantB.Folder)) return;
            var aPath = _variantA.Folder + "/image_metrics_after.json";
            var bPath = _variantB.Folder + "/image_metrics_after.json";
            if (!File.Exists(aPath) || !File.Exists(bPath)) return;
            var a = JsonUtility.FromJson<USD_ImageMetricsFile>(File.ReadAllText(aPath));
            var b = JsonUtility.FromJson<USD_ImageMetricsFile>(File.ReadAllText(bPath));
            if (Mathf.Abs(a.aggregate.luma_mean - b.aggregate.luma_mean) < 0.01f && Mathf.Abs(a.aggregate.saturation_mean - b.aggregate.saturation_mean) < 0.02f && Mathf.Abs(a.aggregate.warm_cool_balance - b.aggregate.warm_cool_balance) < 0.03f)
            {
                SetStatus(MessageType.Warning, "A/B 视觉差异不足，建议提高 Force Diversity 或 temperature。");
            }
        }


        private static Vector3 ClampSvhVector(Vector3 v, float max)
        {
            var mag = v.magnitude;
            if (mag <= max || mag <= 1e-6f) return v;
            return v * (max / mag);
        }

        private void DrawLlmStatus()
        {
            if (_llmStatus.state == USD_LlmCallState.Idle) return;
            var msg = $"[{_llmStatus.state}] {_llmStatus.lastMessage}";
            if (!string.IsNullOrEmpty(_llmStatus.lastError)) msg += "\n" + _llmStatus.lastError;
            var type = _llmStatus.state == USD_LlmCallState.Fail ? MessageType.Error : MessageType.Info;
            EditorGUILayout.HelpBox(msg, type);
        }

        private static string GetStyleDisplay(USD_StyleGoal x) => USD_Loc.CurrentLang() == "zh" ? $"{x.name_zh} ({x.id})" : $"{x.name_en} ({x.id})";
        private void SetStatus(MessageType type, string text) { _statusType = type; _status = text; Debug.Log("[USD][AITuning] " + text); }
    }

}
