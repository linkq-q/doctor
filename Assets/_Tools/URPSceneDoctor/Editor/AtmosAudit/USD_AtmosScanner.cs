using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace URPSceneDoctor.Editor
{
    public static class USD_AtmosScanner
    {
        public static USD_ScanSnapshot CaptureSnapshot()
        {
            var snapshot = new USD_ScanSnapshot();
            snapshot.unityVersion = Application.unityVersion;
            snapshot.urpPackageVersion = GetUrpVersion();
            snapshot.platformHint = EditorUserBuildSettings.activeBuildTarget.ToString();
            snapshot.activeQualityLevel = QualitySettings.names[QualitySettings.GetQualityLevel()];

            try
            {
                var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (urp == null)
                {
                    snapshot.warnings.Add("Current Render Pipeline is not URP asset.");
                    return snapshot;
                }

                var urpPath = AssetDatabase.GetAssetPath(urp);
                snapshot.activeURPAssetGuid = AssetDatabase.AssetPathToGUID(urpPath);
                snapshot.activeURPAssetName = urp.name;
                snapshot.shadowDistance = urp.shadowDistance;
                snapshot.shadowCascadeCount = urp.shadowCascadeCount;
                snapshot.supportsHDR = urp.supportsHDR;
                snapshot.msaaSampleCount = urp.msaaSampleCount;
                snapshot.renderScale = urp.renderScale;
                snapshot.additionalLightsEnabled = urp.additionalLightsRenderingMode != LightRenderingMode.Disabled;
                snapshot.additionalLightsPerObjectLimit = urp.maxAdditionalLightsCount;

                var rendererData = urp.scriptableRendererData;
                if (rendererData != null)
                {
                    var rdPath = AssetDatabase.GetAssetPath(rendererData);
                    snapshot.activeRendererDataGuid = AssetDatabase.AssetPathToGUID(rdPath);
                    snapshot.activeRendererDataName = rendererData.name;
                }
            }
            catch (Exception e)
            {
                snapshot.warnings.Add("Failed to read URP fields: " + e.Message);
            }

            try
            {
                var mainLight = RenderSettings.sun;
                snapshot.hasDirectionalLight = mainLight != null;
                if (mainLight != null)
                {
                    snapshot.directionalLightName = mainLight.name;
                    snapshot.dirLightShadowsEnabled = mainLight.shadows != LightShadows.None;
                    snapshot.dirLightShadowStrength = mainLight.shadowStrength;
                    snapshot.dirLightColorTemperatureEnabled = mainLight.useColorTemperature;
                    snapshot.dirLightTemperature = mainLight.colorTemperature;
                }

                snapshot.ambientMode = RenderSettings.ambientMode.ToString();
                snapshot.ambientIntensity = RenderSettings.ambientIntensity;
                snapshot.hasSkyboxMaterial = RenderSettings.skybox != null;
                var probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
                snapshot.reflectionProbeCount = probes.Length;
                snapshot.hasAnyReflectionProbe = probes.Length > 0;
            }
            catch (Exception e)
            {
                snapshot.warnings.Add("Failed to read lighting fields: " + e.Message);
            }

            try
            {
                var volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
                snapshot.volumeCountTotal = volumes.Length;
                var globals = volumes.Where(v => v != null && v.isGlobal).ToList();
                snapshot.hasGlobalVolume = globals.Count > 0;
                snapshot.hasMultipleOverlappingGlobalLikeVolumes = globals.Count > 1;

                var selected = globals.FirstOrDefault();
                if (selected != null)
                {
                    snapshot.globalVolumeObjectName = selected.gameObject.name;
                    var profile = selected.sharedProfile;
                    if (profile != null)
                    {
                        var profilePath = AssetDatabase.GetAssetPath(profile);
                        snapshot.globalVolumeProfileGuid = AssetDatabase.AssetPathToGUID(profilePath);
                        snapshot.globalVolumeProfileName = profile.name;

                        foreach (var component in profile.components)
                        {
                            if (component != null && component.active)
                            {
                                snapshot.enabledOverrides.Add(component.GetType().Name);
                            }
                        }
                    }
                }

                snapshot.postProcessingEnabled = snapshot.enabledOverrides.Count > 0;
            }
            catch (Exception e)
            {
                snapshot.warnings.Add("Failed to read volume fields: " + e.Message);
            }

            try
            {
                var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                var materials = new HashSet<Material>();
                var shaders = new HashSet<Shader>();
                var transparentCount = 0;
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    var rendererIsTransparent = false;
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat == null) continue;
                        materials.Add(mat);
                        if (mat.shader != null) shaders.Add(mat.shader);
                        if (!rendererIsTransparent && mat.renderQueue >= (int)RenderQueue.Transparent)
                        {
                            rendererIsTransparent = true;
                        }
                    }

                    if (rendererIsTransparent) transparentCount++;
                }

                snapshot.rendererCount = renderers.Length;
                snapshot.materialCountDistinct = materials.Count;
                snapshot.shaderCountDistinct = shaders.Count;
                snapshot.transparentRendererCount = transparentCount;
                snapshot.hasManyDifferentShaders = shaders.Count > 20;
            }
            catch (Exception e)
            {
                snapshot.warnings.Add("Failed to read renderer fields: " + e.Message);
            }

            if (!SceneManager.GetActiveScene().isLoaded || string.IsNullOrWhiteSpace(SceneManager.GetActiveScene().name))
            {
                snapshot.warnings.Add("Active scene is unsaved.");
            }

            return snapshot;
        }

        private static string GetUrpVersion()
        {
            var package = PackageInfo.FindForAssetPath("Packages/com.unity.render-pipelines.universal");
            return package != null ? package.version : "unknown";
        }
    }
}
