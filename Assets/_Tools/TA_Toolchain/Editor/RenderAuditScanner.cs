using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TA.Toolchain.RenderAudit
{
    public sealed class ScanResultEntry
    {
        public Issue issue;
        public UnityEngine.Object pingTarget;
    }

    public sealed class RenderAuditScanResult
    {
        public RenderAuditReport report;
        public List<ScanResultEntry> entries = new List<ScanResultEntry>();
        public List<Suggestion> suggestions = new List<Suggestion>();
    }

    public static class RenderAuditScanner
    {
        public static RenderAuditScanResult Scan(RenderAuditConfig config)
        {
            var report = new RenderAuditReport();
            var result = new RenderAuditScanResult { report = report };
            var thresholds = config.thresholds;

            report.meta.unityVersion = Application.unityVersion;
            report.meta.urpVersion = typeof(UniversalRenderPipelineAsset).Assembly.GetName().Version?.ToString() ?? "Unknown";
            report.meta.sceneName = EditorSceneManager.GetActiveScene().name;
            report.meta.timestamp = DateTime.Now.ToString("o");

            report.summary.thresholds = new ThresholdSnapshot
            {
                realtimeShadowLightsWarn = thresholds.realtimeShadowLightsWarn,
                shadowDistanceWarn = thresholds.shadowDistanceWarn,
                rendererCountWarn = thresholds.rendererCountWarn,
                transparentRendererCountWarn = thresholds.transparentRendererCountWarn,
                particleSystemCountWarn = thresholds.particleSystemCountWarn,
                realtimeReflectionProbeWarn = thresholds.realtimeReflectionProbeWarn,
                textureMaxSizeWarn = thresholds.textureMaxSizeWarn,
                texture2kCountWarn = thresholds.texture2kCountWarn,
                texture4kCountWarn = thresholds.texture4kCountWarn,
                triWarn = thresholds.triWarn,
                vertWarn = thresholds.vertWarn,
                topNTextures = thresholds.topNTextures,
                topNMeshes = thresholds.topNMeshes
            };

            ScanScene(config, result);
            ScanUrp(config, result);
            ScanMaterialsAndTextures(config, result);
            ScanMeshes(config, result);
            ScanMissingScripts(config, result);

            if (config.enableRecommendations)
            {
                result.suggestions = RecommendationEngine.GenerateSuggestions(config);
                foreach (var suggestion in result.suggestions)
                {
                    AddIssue(result, new Issue
                    {
                        id = Guid.NewGuid().ToString("N"),
                        severity = IssueSeverity.Info,
                        category = IssueCategory.Recommendation,
                        title = suggestion.title,
                        detail = $"{suggestion.why} Cost: {suggestion.estimatedCost}. Where: {suggestion.whereToImplement}. Missing evidence: {suggestion.evidenceMissing}",
                        targetType = TargetType.ProjectSetting,
                        targetPath = "Project",
                        suggestionSteps = new[] { suggestion.whereToImplement }
                    });
                }
            }

            PopulateSummary(result.report);
            return result;
        }

        private static void ScanScene(RenderAuditConfig config, RenderAuditScanResult result)
        {
            var report = result.report;
            var thresholds = config.thresholds;

            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
            var lights = UnityEngine.Object.FindObjectsOfType<Light>(true);
            var particles = UnityEngine.Object.FindObjectsOfType<ParticleSystem>(true);
            var probes = UnityEngine.Object.FindObjectsOfType<ReflectionProbe>(true);

            var transparentRendererCount = 0;
            foreach (var renderer in renderers)
            {
                var mats = renderer.sharedMaterials;
                var isTransparent = mats.Any(m => m != null && (m.renderQueue >= (int)RenderQueue.Transparent || IsTransparentBlend(m)));
                if (isTransparent)
                {
                    transparentRendererCount++;
                }

                report.topOffenders.renderers.Add(new RendererOffender
                {
                    name = renderer.name,
                    hierarchyPath = GetHierarchyPath(renderer.transform),
                    materialCount = mats?.Length ?? 0,
                    transparent = isTransparent
                });
            }

            foreach (var light in lights)
            {
                report.topOffenders.lights.Add(new LightOffender
                {
                    name = light.name,
                    hierarchyPath = GetHierarchyPath(light.transform),
                    type = light.type.ToString(),
                    realtimeShadows = light.shadows != LightShadows.None && light.lightmapBakeType == LightmapBakeType.Realtime,
                    intensity = light.intensity
                });
            }

            report.summary.rendererCount = renderers.Length;
            report.summary.transparentRendererCount = transparentRendererCount;
            report.summary.lightCount = lights.Length;
            report.summary.particleSystemCount = particles.Length;
            report.summary.reflectionProbeCount = probes.Length;
            report.summary.realtimeShadowLightCount = lights.Count(l => l.shadows != LightShadows.None && l.lightmapBakeType == LightmapBakeType.Realtime);

            if (report.summary.rendererCount > thresholds.rendererCountWarn)
            {
                AddIssue(result, NewWarning(IssueCategory.Performance, "Renderer count high", $"Renderer count {report.summary.rendererCount} exceeds {thresholds.rendererCountWarn}.", TargetType.ProjectSetting, report.meta.sceneName));
            }

            if (report.summary.transparentRendererCount > thresholds.transparentRendererCountWarn)
            {
                AddIssue(result, NewWarning(IssueCategory.Transparency, "Transparent renderer count high", $"Transparent renderer count {report.summary.transparentRendererCount} exceeds {thresholds.transparentRendererCountWarn}.", TargetType.ProjectSetting, report.meta.sceneName));
            }

            if (report.summary.particleSystemCount > thresholds.particleSystemCountWarn)
            {
                AddIssue(result, NewWarning(IssueCategory.Performance, "Particle system count high", $"ParticleSystem count {report.summary.particleSystemCount} exceeds {thresholds.particleSystemCountWarn}.", TargetType.ProjectSetting, report.meta.sceneName));
            }

            if (report.summary.reflectionProbeCount > thresholds.realtimeReflectionProbeWarn)
            {
                AddIssue(result, NewWarning(IssueCategory.Performance, "Reflection probe count high", $"ReflectionProbe count {report.summary.reflectionProbeCount} exceeds {thresholds.realtimeReflectionProbeWarn}.", TargetType.ProjectSetting, report.meta.sceneName));
            }

            if (report.summary.realtimeShadowLightCount > thresholds.realtimeShadowLightsWarn)
            {
                AddIssue(result, NewWarning(IssueCategory.Performance, "Realtime shadow lights high", $"Realtime shadow-casting lights {report.summary.realtimeShadowLightCount} exceeds {thresholds.realtimeShadowLightsWarn}.", TargetType.ProjectSetting, report.meta.sceneName));
            }
        }

        private static void ScanUrp(RenderAuditConfig config, RenderAuditScanResult result)
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                AddIssue(result, NewWarning(IssueCategory.URP, "No URP asset active", "Current render pipeline is not UniversalRenderPipelineAsset.", TargetType.ProjectSetting, "ProjectSettings/Graphics"));
                return;
            }

            var threshold = config.thresholds.shadowDistanceWarn;
            if (urp.shadowDistance > threshold)
            {
                AddIssue(result, NewWarning(IssueCategory.URP, "Shadow distance high", $"URP shadowDistance {urp.shadowDistance:F1} exceeds {threshold:F1}.", TargetType.ProjectSetting, "URP Asset"));
            }

            AddIssue(result, new Issue
            {
                id = Guid.NewGuid().ToString("N"),
                severity = IssueSeverity.Info,
                category = IssueCategory.URP,
                title = "URP shadow settings",
                detail = $"Cascade count {urp.shadowCascadeCount}, mainLightShadowsSupported={urp.supportsMainLightShadows}, additionalLightShadowsSupported={urp.supportsAdditionalLightShadows}.",
                targetType = TargetType.ProjectSetting,
                targetPath = "URP Asset"
            });

            if (!config.enableSSRDetect)
            {
                return;
            }

            var so = new SerializedObject(urp);
            var rendererList = so.FindProperty("m_RendererDataList");
            if (rendererList == null || !rendererList.isArray)
            {
                return;
            }

            for (var i = 0; i < rendererList.arraySize; i++)
            {
                var data = rendererList.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
                if (data == null)
                {
                    continue;
                }

                foreach (var feature in data.rendererFeatures)
                {
                    if (feature == null)
                    {
                        continue;
                    }

                    var typeName = feature.GetType().Name;
                    var featureName = feature.name;
                    if (typeName.IndexOf("ScreenSpaceReflection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        typeName.IndexOf("SSR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        featureName.IndexOf("ScreenSpaceReflection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        featureName.IndexOf("SSR", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        AddIssue(result, new Issue
                        {
                            id = Guid.NewGuid().ToString("N"),
                            severity = IssueSeverity.Warning,
                            category = IssueCategory.SSR,
                            title = "SSR renderer feature detected",
                            detail = $"Renderer feature '{featureName}' ({typeName}) may add significant cost.",
                            targetType = TargetType.Asset,
                            targetPath = AssetDatabase.GetAssetPath(feature),
                            targetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(feature))
                        }, feature);
                    }
                }
            }
        }

        private static void ScanMaterialsAndTextures(RenderAuditConfig config, RenderAuditScanResult result)
        {
            var thresholds = config.thresholds;
            var materialGuids = AssetDatabase.FindAssets("t:Material");
            var textures = new Dictionary<string, TextureOffender>(StringComparer.OrdinalIgnoreCase);
            var tex2kCount = 0;
            var tex4kCount = 0;

            foreach (var guid in materialGuids)
            {
                var matPath = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (material == null)
                {
                    continue;
                }

                if (material.shader == null)
                {
                    AddIssue(result, new Issue
                    {
                        id = Guid.NewGuid().ToString("N"),
                        severity = IssueSeverity.Error,
                        category = IssueCategory.Material,
                        title = "Material missing shader",
                        detail = $"Material '{material.name}' has null shader reference.",
                        targetType = TargetType.Asset,
                        targetPath = matPath,
                        targetGuid = guid
                    }, material);
                    continue;
                }

                if (config.enableTransparencySortingDetect && (material.renderQueue >= (int)RenderQueue.Transparent || IsTransparentBlend(material)))
                {
                    AddIssue(result, new Issue
                    {
                        id = Guid.NewGuid().ToString("N"),
                        severity = IssueSeverity.Info,
                        category = IssueCategory.Transparency,
                        title = "Transparent material detected",
                        detail = $"Material '{material.name}' uses transparent queue/blend; verify sorting in layered shots.",
                        targetType = TargetType.Asset,
                        targetPath = matPath,
                        targetGuid = guid
                    }, material);
                }

                var shader = material.shader;
                var propCount = shader.GetPropertyCount();
                for (var i = 0; i < propCount; i++)
                {
                    if (shader.GetPropertyType(i) != ShaderPropertyType.Texture)
                    {
                        continue;
                    }

                    var propName = shader.GetPropertyName(i);
                    var tex = material.GetTexture(propName) as Texture2D;
                    if (tex == null)
                    {
                        continue;
                    }

                    var texPath = AssetDatabase.GetAssetPath(tex);
                    if (string.IsNullOrEmpty(texPath))
                    {
                        continue;
                    }

                    if (!textures.ContainsKey(texPath))
                    {
                        var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                        var maxSize = importer != null ? importer.maxTextureSize : Math.Max(tex.width, tex.height);
                        if (maxSize >= 4096) tex4kCount++;
                        else if (maxSize >= 2048) tex2kCount++;

                        var estimatedBytes = EstimateTextureBytes(tex, importer);
                        textures[texPath] = new TextureOffender
                        {
                            assetPath = texPath,
                            guid = AssetDatabase.AssetPathToGUID(texPath),
                            width = tex.width,
                            height = tex.height,
                            maxSize = maxSize,
                            mipmap = importer != null && importer.mipmapEnabled,
                            sRGB = importer == null || importer.sRGBTexture,
                            compression = importer != null ? importer.textureCompression.ToString() : "Unknown",
                            estimatedBytes = estimatedBytes
                        };

                        if (maxSize >= thresholds.textureMaxSizeWarn)
                        {
                            AddIssue(result, new Issue
                            {
                                id = Guid.NewGuid().ToString("N"),
                                severity = IssueSeverity.Warning,
                                category = IssueCategory.Texture,
                                title = "Large texture max size",
                                detail = $"Texture '{Path.GetFileName(texPath)}' maxSize {maxSize} meets/exceeds {thresholds.textureMaxSizeWarn}.",
                                targetType = TargetType.Asset,
                                targetPath = texPath,
                                targetGuid = AssetDatabase.AssetPathToGUID(texPath),
                                suggestionSteps = new[] { "Reduce Max Size or use streaming/atlasing for distant assets." }
                            }, tex);
                        }
                    }
                }
            }

            if (tex2kCount > thresholds.texture2kCountWarn)
            {
                AddIssue(result, NewWarning(IssueCategory.Texture, "2K texture count high", $"2K texture count {tex2kCount} exceeds {thresholds.texture2kCountWarn}.", TargetType.ProjectSetting, "Assets"));
            }

            if (tex4kCount > thresholds.texture4kCountWarn)
            {
                AddIssue(result, NewWarning(IssueCategory.Texture, "4K texture count high", $"4K texture count {tex4kCount} exceeds {thresholds.texture4kCountWarn}.", TargetType.ProjectSetting, "Assets"));
            }

            result.report.topOffenders.textures = textures.Values
                .OrderByDescending(t => t.estimatedBytes)
                .ThenBy(t => t.assetPath, StringComparer.Ordinal)
                .Take(Math.Max(1, thresholds.topNTextures))
                .ToList();
        }

        private static void ScanMeshes(RenderAuditConfig config, RenderAuditScanResult result)
        {
            var thresholds = config.thresholds;
            var meshItems = new List<MeshOffender>();
            var unreadableMeshFilterCount = 0;
            var unreadableSkinnedMeshCount = 0;

            foreach (var mf in UnityEngine.Object.FindObjectsOfType<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                if (!TryGetTriangleCount(mesh, out var triangleCount))
                {
                    unreadableMeshFilterCount++;
                    continue;
                }

                meshItems.Add(new MeshOffender
                {
                    name = mesh.name,
                    hierarchyPath = GetHierarchyPath(mf.transform),
                    triangles = triangleCount,
                    vertices = mesh.vertexCount
                });
            }

            foreach (var smr in UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                if (!TryGetTriangleCount(mesh, out var triangleCount))
                {
                    unreadableSkinnedMeshCount++;
                    continue;
                }

                meshItems.Add(new MeshOffender
                {
                    name = mesh.name,
                    hierarchyPath = GetHierarchyPath(smr.transform),
                    triangles = triangleCount,
                    vertices = mesh.vertexCount
                });
            }

            foreach (var item in meshItems)
            {
                if (item.triangles > thresholds.triWarn || item.vertices > thresholds.vertWarn)
                {
                    AddIssue(result, new Issue
                    {
                        id = Guid.NewGuid().ToString("N"),
                        severity = IssueSeverity.Warning,
                        category = IssueCategory.Performance,
                        title = "High poly mesh in scene",
                        detail = $"{item.hierarchyPath} has mesh {item.name} with {item.triangles} tris / {item.vertices} verts.",
                        targetType = TargetType.SceneObject,
                        targetPath = item.hierarchyPath,
                        suggestionSteps = new[] { "Use LODGroup or lower-poly variant for mid/far distance." }
                    });
                }
            }

            result.report.topOffenders.meshes = meshItems
                .OrderByDescending(m => m.triangles)
                .ThenBy(m => m.hierarchyPath, StringComparer.Ordinal)
                .Take(Math.Max(1, thresholds.topNMeshes))
                .ToList();

            var totalUnreadableMeshes = unreadableMeshFilterCount + unreadableSkinnedMeshCount;
            if (totalUnreadableMeshes > 0)
            {
                AddIssue(result, NewInfo(
                    IssueCategory.Performance,
                    "Skipped non-readable meshes during triangle scan",
                    $"Skipped {totalUnreadableMeshes} mesh reference(s) because Read/Write is disabled or triangle data could not be read ({unreadableMeshFilterCount} MeshFilter, {unreadableSkinnedMeshCount} SkinnedMeshRenderer).",
                    TargetType.ProjectSetting,
                    "Mesh Import Settings"));
            }
        }

        private static bool TryGetTriangleCount(Mesh mesh, out int triangleCount)
        {
            triangleCount = 0;
            if (!mesh.isReadable)
            {
                return false;
            }

            try
            {
                triangleCount = mesh.triangles.Length / 3;
                return true;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private static void ScanMissingScripts(RenderAuditConfig config, RenderAuditScanResult result)
        {
            var roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var so = new SerializedObject(t.gameObject);
                    var comps = so.FindProperty("m_Component");
                    if (comps == null || !comps.isArray)
                    {
                        continue;
                    }

                    for (var i = 0; i < comps.arraySize; i++)
                    {
                        var comp = comps.GetArrayElementAtIndex(i);
                        var refProp = comp.FindPropertyRelative("component");
                        if (refProp != null && refProp.objectReferenceValue == null)
                        {
                            AddIssue(result, new Issue
                            {
                                id = Guid.NewGuid().ToString("N"),
                                severity = IssueSeverity.Error,
                                category = IssueCategory.Reference,
                                title = "Missing MonoBehaviour script",
                                detail = $"GameObject '{t.gameObject.name}' has a missing script reference.",
                                targetType = TargetType.SceneObject,
                                targetPath = GetHierarchyPath(t),
                                suggestionSteps = new[] { "Remove missing components or restore the script GUID." }
                            }, t.gameObject);
                            break;
                        }
                    }
                }
            }
        }

        private static void AddIssue(RenderAuditScanResult result, Issue issue, UnityEngine.Object target = null)
        {
            result.report.issues.Add(issue);
            result.entries.Add(new ScanResultEntry { issue = issue, pingTarget = target });
        }

        private static Issue NewWarning(IssueCategory category, string title, string detail, TargetType targetType, string targetPath)
        {
            return new Issue
            {
                id = Guid.NewGuid().ToString("N"),
                severity = IssueSeverity.Warning,
                category = category,
                title = title,
                detail = detail,
                targetType = targetType,
                targetPath = targetPath
            };
        }

        private static bool IsTransparentBlend(Material material)
        {
            if (material == null)
            {
                return false;
            }

            return material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") || material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON") || material.IsKeywordEnabled("_ALPHATEST_ON");
        }

        private static long EstimateTextureBytes(Texture2D texture, TextureImporter importer)
        {
            var width = Math.Max(1, texture.width);
            var height = Math.Max(1, texture.height);
            var bpp = 4; // conservative default

            if (importer != null)
            {
                switch (importer.textureCompression)
                {
                    case TextureImporterCompression.Uncompressed:
                        bpp = 4;
                        break;
                    case TextureImporterCompression.Compressed:
                    case TextureImporterCompression.CompressedHQ:
                    case TextureImporterCompression.CompressedLQ:
                        bpp = 1;
                        break;
                }
            }

            var size = (long)width * height * bpp;
            if (importer != null && importer.mipmapEnabled)
            {
                size = (long)(size * 1.33f);
            }

            return size;
        }

        private static string GetHierarchyPath(Transform target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            var stack = new Stack<string>();
            var current = target;
            while (current != null)
            {
                stack.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", stack);
        }

        private static void PopulateSummary(RenderAuditReport report)
        {
            report.summary.issueCount = report.issues.Count;
            report.summary.errorCount = report.issues.Count(i => i.severity == IssueSeverity.Error);
            report.summary.warningCount = report.issues.Count(i => i.severity == IssueSeverity.Warning);
            report.summary.infoCount = report.issues.Count(i => i.severity == IssueSeverity.Info);

            report.topOffenders.renderers = report.topOffenders.renderers
                .OrderByDescending(r => r.materialCount)
                .ThenBy(r => r.hierarchyPath, StringComparer.Ordinal)
                .Take(20)
                .ToList();

            report.topOffenders.lights = report.topOffenders.lights
                .OrderByDescending(l => l.realtimeShadows)
                .ThenByDescending(l => l.intensity)
                .ThenBy(l => l.hierarchyPath, StringComparer.Ordinal)
                .Take(20)
                .ToList();
        }
    }
}
