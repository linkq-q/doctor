using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using URPSceneDoctor.Runtime;
using UPM_PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace URPSceneDoctor.Editor
{
    public enum USD_CheckLevel { Pass, Warning, Fail }

    [Serializable]
    public sealed class USD_PipelineCheck
    {
        public string id;
        public string title;
        public USD_CheckLevel level;
        public string detail;
    }

    [Serializable]
    public sealed class USD_PipelinePreflightResult
    {
        public string packName = "Base Pack v1";
        public bool installed;
        public string urpVersion;
        public List<USD_PipelineCheck> checks = new List<USD_PipelineCheck>();

        public int P0Count => checks.Count(x => x.level == USD_CheckLevel.Fail);
        public int P1Count => checks.Count(x => x.level == USD_CheckLevel.Warning);
    }

    public static class USD_PipelinePackUtil
    {
        public const string Root = "Assets/_Tools/URPSceneDoctor/PipelinePack";
        public const string FogAssetDir = Root + "/DistanceFog";
        public const string VolAssetDir = Root + "/VolumetricLight";

        public static USD_PipelinePreflightResult RunPreflight()
        {
            var r = new USD_PipelinePreflightResult { urpVersion = GetUrpVersion() };
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                r.checks.Add(Fail("urp.active", "URP Pipeline", "Current Render Pipeline is not URP."));
                return r;
            }

            r.checks.Add(Pass("urp.active", "URP Pipeline", "URP is active."));

            var rendererData = GetRendererData(urp);
            if (rendererData == null)
            {
                r.checks.Add(Warn("urp.rendererData", "RendererData", "RendererData not accessible on this URP version."));
            }
            else
            {
                r.checks.Add(Pass("urp.rendererData", "RendererData", rendererData.name));
            }

            r.checks.Add(CheckPostProcessHint());
            r.checks.Add(CheckOpaqueDepth(urp));

            var hasFog = HasFeature<USD_DistanceFogFeature>(rendererData);
            var hasVol = HasFeature<USD_VolumetricLightFeature>(rendererData);
            r.checks.Add(hasFog ? Pass("fog.feature", "Distance Fog Feature", "Installed") : Warn("fog.feature", "Distance Fog Feature", "Not installed"));
            r.checks.Add(hasVol ? Pass("vol.feature", "Volumetric Light Feature", "Installed") : Warn("vol.feature", "Volumetric Light Feature", "Not installed"));
            r.checks.Add(CheckVolThirdPartyFolder());

            r.installed = hasFog && hasVol;
            return r;
        }

        public static List<string> InstallBasePackV1(string sceneName, string timestamp)
        {
            var changes = new List<string>();
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                changes.Add("Install aborted: URP not active.");
                return changes;
            }

            var rendererData = GetRendererData(urp);
            if (rendererData == null)
            {
                changes.Add("Install aborted: RendererData not accessible.");
                return changes;
            }

            USD_EditorUtil.EnsureFolder(FogAssetDir);
            USD_EditorUtil.EnsureFolder(VolAssetDir);

            var fogSettingsPath = USD_EditorUtil.EnsureUniqueAssetPath($"{FogAssetDir}/{timestamp}_DistanceFogSettings.asset");
            var fogSettings = ScriptableObject.CreateInstance<USD_DistanceFogSettings>();
            AssetDatabase.CreateAsset(fogSettings, fogSettingsPath);
            changes.Add("Created: " + fogSettingsPath);

            var volSettingsPath = USD_EditorUtil.EnsureUniqueAssetPath($"{VolAssetDir}/{timestamp}_VolumetricLightSettings.asset");
            var volSettings = ScriptableObject.CreateInstance<USD_VolumetricLightSettings>();
            AssetDatabase.CreateAsset(volSettings, volSettingsPath);
            changes.Add("Created: " + volSettingsPath);

            Undo.RecordObject(rendererData, "Install Pipeline Base Pack v1");

            if (!HasFeature<USD_DistanceFogFeature>(rendererData))
            {
                var fogFeature = ScriptableObject.CreateInstance<USD_DistanceFogFeature>();
                fogFeature.name = "USD_DistanceFogFeature";
                fogFeature.SetSettings(fogSettings);
                AddRendererFeature(rendererData, fogFeature);
                changes.Add("Installed renderer feature: USD_DistanceFogFeature");
            }
            else
            {
                changes.Add("Distance Fog feature already installed.");
            }

            if (!HasFeature<USD_VolumetricLightFeature>(rendererData))
            {
                var volFeature = ScriptableObject.CreateInstance<USD_VolumetricLightFeature>();
                volFeature.name = "USD_VolumetricLightFeature";
                volFeature.SetSettings(volSettings);
                AddRendererFeature(rendererData, volFeature);
                changes.Add("Installed renderer feature: USD_VolumetricLightFeature");
            }
            else
            {
                changes.Add("Volumetric Light feature already installed.");
            }

            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            return changes;
        }

        public static List<string> UninstallBasePackV1()
        {
            var changes = new List<string>();
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                changes.Add("Uninstall skipped: URP not active.");
                return changes;
            }

            var rendererData = GetRendererData(urp);
            if (rendererData == null)
            {
                changes.Add("Uninstall skipped: RendererData not accessible.");
                return changes;
            }

            Undo.RecordObject(rendererData, "Uninstall Pipeline Base Pack v1");
            RemoveFeaturesOfType<USD_DistanceFogFeature>(rendererData, changes);
            RemoveFeaturesOfType<USD_VolumetricLightFeature>(rendererData, changes);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            return changes;
        }

        private static USD_PipelineCheck CheckPostProcessHint()
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            var anyPost = cameras.Any(c => c != null && c.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.renderPostProcessing);
            return anyPost
                ? Pass("camera.post", "Camera Post Processing", "At least one camera has post-processing enabled.")
                : Warn("camera.post", "Camera Post Processing", "No camera with post-processing enabled (hint only).");
        }

        private static USD_PipelineCheck CheckOpaqueDepth(UniversalRenderPipelineAsset urp)
        {
            var opaque = urp.supportsCameraOpaqueTexture;
            var depth = urp.supportsCameraDepthTexture;
            if (opaque && depth) return Pass("fog.prereq", "Distance Fog prerequisites", "Opaque/Depth textures are enabled.");
            return Warn("fog.prereq", "Distance Fog prerequisites", $"OpaqueTexture={opaque}, DepthTexture={depth}. Some fog variants may require both.");
        }

        private static USD_PipelineCheck CheckVolThirdPartyFolder()
        {
            var exists = AssetDatabase.IsValidFolder("Assets/_Tools/URPSceneDoctor/ThirdParty/URPVolumetricLight");
            return exists
                ? Pass("vol.vendor", "Volumetric Light vendored folder", "Found third-party folder.")
                : Warn("vol.vendor", "Volumetric Light vendored folder", "Missing vendored package folder.");
        }

        private static void RemoveFeaturesOfType<T>(ScriptableRendererData rendererData, List<string> changes) where T : ScriptableRendererFeature
        {
            var list = rendererData.rendererFeatures;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is T)
                {
                    changes.Add("Removed renderer feature: " + list[i].name);
                    Undo.DestroyObjectImmediate(list[i]);
                    list.RemoveAt(i);
                }
            }
        }

        private static void AddRendererFeature(ScriptableRendererData rendererData, ScriptableRendererFeature feature)
        {
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            rendererData.rendererFeatures.Add(feature);
            Undo.RegisterCreatedObjectUndo(feature, "Add Renderer Feature");
            EditorUtility.SetDirty(feature);
        }

        private static bool HasFeature<T>(ScriptableRendererData rendererData) where T : ScriptableRendererFeature
        {
            if (rendererData == null) return false;
            return rendererData.rendererFeatures.Any(f => f is T);
        }

        private static ScriptableRendererData GetRendererData(UniversalRenderPipelineAsset urp)
        {
            var t = urp.GetType();
            var prop = t.GetProperty("scriptableRendererData", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                var value = prop.GetValue(urp) as ScriptableRendererData;
                if (value != null) return value;
            }

            var prop2 = t.GetProperty("rendererData", BindingFlags.Public | BindingFlags.Instance);
            if (prop2 != null)
            {
                return prop2.GetValue(urp) as ScriptableRendererData;
            }

            return null;
        }

        private static string GetUrpVersion()
        {
            try
            {
                var p = UPM_PackageInfo.FindForAssetPath("Packages/com.unity.render-pipelines.universal");
                return p != null ? p.version : "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private static USD_PipelineCheck Pass(string id, string title, string detail) => new USD_PipelineCheck { id = id, title = title, detail = detail, level = USD_CheckLevel.Pass };
        private static USD_PipelineCheck Warn(string id, string title, string detail) => new USD_PipelineCheck { id = id, title = title, detail = detail, level = USD_CheckLevel.Warning };
        private static USD_PipelineCheck Fail(string id, string title, string detail) => new USD_PipelineCheck { id = id, title = title, detail = detail, level = USD_CheckLevel.Fail };
    }
}
