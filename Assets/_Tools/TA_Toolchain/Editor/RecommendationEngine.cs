using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TA.Toolchain.RenderAudit
{
    public static class RecommendationEngine
    {
        private static readonly string[] ScriptKeywords =
        {
            "ScriptableRendererFeature", "DepthOfField", "Outline", "Fog", "OcclusionCulling",
            "LODGroup", "ScreenSpaceAmbientOcclusion", "Bloom", "ColorAdjustments", "Tonemapping"
        };

        public static List<Suggestion> GenerateSuggestions(RenderAuditConfig config)
        {
            var capabilities = ScanCapabilities(config.enableScriptKeywordScan);
            var suggestions = new List<Suggestion>();

            AddIfMissing(suggestions, !capabilities.HasDoF, "Depth of Field", "DoF can improve focus and perceived scene depth.", EstimatedCost.Med, "URP Volume", capabilities.DoFEvidence);
            AddIfMissing(suggestions, !capabilities.HasSSAO, "SSAO", "SSAO can ground objects and improve contact shadows.", EstimatedCost.Med, "URP Volume / RendererFeature", capabilities.SSAOEvidence);
            AddIfMissing(suggestions, !capabilities.HasOutline, "Outline", "Outline helps readability for interactables and style targets.", EstimatedCost.Low, "RendererFeature / Shader", capabilities.OutlineEvidence);
            AddIfMissing(suggestions, !capabilities.HasFog, "Fog", "Fog supports depth cueing and atmosphere.", EstimatedCost.Low, "URP Volume / RendererFeature", capabilities.FogEvidence);
            AddIfMissing(suggestions, !capabilities.HasColorGrading, "LUT / Color Grading", "Color grading improves visual consistency and tone.", EstimatedCost.Low, "URP Volume", capabilities.ColorEvidence);
            AddIfMissing(suggestions, !capabilities.HasLODOrOcclusion, "LOD / Occlusion", "LODGroup and occlusion reduce hidden or distant rendering cost.", EstimatedCost.Med, "Scene setup / Project Settings", capabilities.LODEvidence);
            AddIfMissing(suggestions, !capabilities.HasLightmapMixed, "Lightmap / Mixed Lighting", "Baked or mixed lighting can reduce realtime shadow overhead.", EstimatedCost.High, "Lighting Settings", capabilities.LightmapEvidence);
            AddIfMissing(suggestions, !capabilities.ShadowBudgetHealthy, "Shadow Budget", "Shadow distance/cascades are expensive; tune by camera need.", EstimatedCost.Med, "URP Asset", capabilities.ShadowEvidence);
            AddIfMissing(suggestions, !capabilities.HasBatcherInstancing, "SRP Batcher / GPU Instancing", "Batching reduces CPU render thread overhead.", EstimatedCost.Low, "URP Asset / Material", capabilities.BatcherEvidence);

            return suggestions;
        }

        private static void AddIfMissing(List<Suggestion> suggestions, bool missing, string title, string why, EstimatedCost cost, string where, string evidence)
        {
            if (!missing)
            {
                return;
            }

            suggestions.Add(new Suggestion
            {
                title = title,
                why = why,
                estimatedCost = cost,
                whereToImplement = where,
                evidenceMissing = string.IsNullOrEmpty(evidence) ? "No capability evidence found." : evidence
            });
        }

        private static CapabilityScanResult ScanCapabilities(bool doScriptKeywordScan)
        {
            var result = new CapabilityScanResult();
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                result.HasBatcherInstancing = GraphicsSettings.useScriptableRenderPipelineBatching;
                result.BatcherEvidence = $"GraphicsSettings.useScriptableRenderPipelineBatching={GraphicsSettings.useScriptableRenderPipelineBatching}";
                result.ShadowBudgetHealthy = urp.shadowDistance <= 60f;
                result.ShadowEvidence = $"shadowDistance={urp.shadowDistance}, cascades={urp.shadowCascadeCount}";
                result.HasOpaqueTexture = urp.supportsCameraOpaqueTexture;
                result.HasDepthTexture = urp.supportsCameraDepthTexture;
            }

            foreach (var volume in UnityEngine.Object.FindObjectsOfType<Volume>(true))
            {
                var profile = volume.sharedProfile;
                if (profile == null)
                {
                    continue;
                }

                foreach (var comp in profile.components)
                {
                    var name = comp.GetType().Name;
                    if (name.IndexOf("DepthOfField", StringComparison.OrdinalIgnoreCase) >= 0) { result.HasDoF = true; result.DoFEvidence = profile.name; }
                    if (name.IndexOf("AmbientOcclusion", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("SSAO", StringComparison.OrdinalIgnoreCase) >= 0) { result.HasSSAO = true; result.SSAOEvidence = profile.name; }
                    if (name.IndexOf("Bloom", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("ColorAdjustments", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Tonemapping", StringComparison.OrdinalIgnoreCase) >= 0) { result.HasColorGrading = true; result.ColorEvidence = profile.name; }
                    if (name.IndexOf("Fog", StringComparison.OrdinalIgnoreCase) >= 0) { result.HasFog = true; result.FogEvidence = profile.name; }
                }
            }

            var rendererFeatures = FindRendererFeatures();
            foreach (var feature in rendererFeatures)
            {
                var n = feature.GetType().Name + " " + feature.name;
                if (Contains(n, "Outline")) { result.HasOutline = true; result.OutlineEvidence = feature.name; }
                if (Contains(n, "Fog")) { result.HasFog = true; result.FogEvidence = feature.name; }
                if (Contains(n, "SSAO") || Contains(n, "AmbientOcclusion")) { result.HasSSAO = true; result.SSAOEvidence = feature.name; }
            }

            var lod = UnityEngine.Object.FindObjectsOfType<LODGroup>(true);
            result.HasLODOrOcclusion = lod.Length > 0;
            result.LODEvidence = lod.Length > 0 ? $"LODGroup count={lod.Length}" : "No LODGroup found";

            var lights = UnityEngine.Object.FindObjectsOfType<Light>(true);
            result.HasLightmapMixed = lights.Any(l => l.lightmapBakeType != LightmapBakeType.Realtime);
            result.LightmapEvidence = result.HasLightmapMixed ? "Found Mixed/Baked lights" : "All lights are Realtime";

            if (doScriptKeywordScan)
            {
                var evidence = ScriptKeywordScan();
                if (!result.HasOutline && evidence.Any(e => e.keyword == "Outline")) { result.HasOutline = true; result.OutlineEvidence = evidence.First(e => e.keyword == "Outline").path; }
                if (!result.HasFog && evidence.Any(e => e.keyword == "Fog")) { result.HasFog = true; result.FogEvidence = evidence.First(e => e.keyword == "Fog").path; }
                if (!result.HasLODOrOcclusion && evidence.Any(e => e.keyword == "LODGroup" || e.keyword == "OcclusionCulling")) { result.HasLODOrOcclusion = true; result.LODEvidence = evidence.First(e => e.keyword == "LODGroup" || e.keyword == "OcclusionCulling").path; }
                if (!result.HasSSAO && evidence.Any(e => e.keyword.Contains("Occlusion"))) { result.HasSSAO = true; result.SSAOEvidence = evidence.First(e => e.keyword.Contains("Occlusion")).path; }
            }

            return result;
        }

        private static List<ScriptableRendererFeature> FindRendererFeatures()
        {
            var list = new List<ScriptableRendererFeature>();
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                return list;
            }

            var so = new SerializedObject(urp);
            var rendererList = so.FindProperty("m_RendererDataList");
            if (rendererList == null || !rendererList.isArray)
            {
                return list;
            }

            for (var i = 0; i < rendererList.arraySize; i++)
            {
                var data = rendererList.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
                if (data == null)
                {
                    continue;
                }
                list.AddRange(data.rendererFeatures.Where(f => f != null));
            }
            return list;
        }

        private static List<CapabilityEvidence> ScriptKeywordScan()
        {
            var list = new List<CapabilityEvidence>();
            var files = Directory.GetFiles(Path.GetFullPath("Assets"), "*.cs", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                string content;
                try
                {
                    content = File.ReadAllText(file);
                }
                catch
                {
                    continue;
                }

                foreach (var keyword in ScriptKeywords)
                {
                    if (!Contains(content, keyword))
                    {
                        continue;
                    }

                    var projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                    var relative = file.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase)
                        ? file.Substring(projectPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/')
                        : file;

                    list.Add(new CapabilityEvidence { path = relative, keyword = keyword });
                }
            }

            return list;
        }

        private static bool Contains(string source, string token)
        {
            return source?.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class CapabilityScanResult
        {
            public bool HasDepthTexture;
            public bool HasOpaqueTexture;
            public bool HasDoF;
            public bool HasSSAO;
            public bool HasOutline;
            public bool HasFog;
            public bool HasColorGrading;
            public bool HasLODOrOcclusion;
            public bool HasLightmapMixed;
            public bool ShadowBudgetHealthy;
            public bool HasBatcherInstancing;

            public string DoFEvidence;
            public string SSAOEvidence;
            public string OutlineEvidence;
            public string FogEvidence;
            public string ColorEvidence;
            public string LODEvidence;
            public string LightmapEvidence;
            public string ShadowEvidence;
            public string BatcherEvidence;
        }
    }
}
