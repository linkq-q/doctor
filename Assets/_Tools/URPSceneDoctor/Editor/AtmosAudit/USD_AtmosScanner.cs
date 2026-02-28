using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UPM_PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace URPSceneDoctor.Editor
{
    public static class USD_AtmosScanner
    {
        public static USD_ScanSnapshot CaptureSnapshot()
        {
            var snapshot = new USD_ScanSnapshot
            {
                unityVersion = Application.unityVersion,
                urpPackageVersion = GetUrpVersion(),
                platformHint = EditorUserBuildSettings.activeBuildTarget.ToString()
            };

            try
            {
                int q = QualitySettings.GetQualityLevel();
                snapshot.activeQualityLevel = (q >= 0 && q < QualitySettings.names.Length) ? QualitySettings.names[q] : q.ToString();
            }
            catch
            {
                snapshot.activeQualityLevel = "unknown";
            }

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
                TryCaptureRendererData(snapshot, urp);
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

                var selected = globals.FirstOrDefault(v => v.gameObject.name == "USD_GlobalVolume") ?? globals.FirstOrDefault();
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
                            if (component != null && component.active) snapshot.enabledOverrides.Add(component.GetType().Name);
                        }

                        FillVolumeKeyValues(snapshot, profile);
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
                        if (!rendererIsTransparent && mat.renderQueue >= (int)RenderQueue.Transparent) rendererIsTransparent = true;
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

            try
            {
                var s = SceneManager.GetActiveScene();
                if (!s.isLoaded || string.IsNullOrWhiteSpace(s.name)) snapshot.warnings.Add("Active scene is unsaved.");
            }
            catch
            {
                snapshot.warnings.Add("Failed to read active scene info.");
            }

            return snapshot;
        }

        private static void FillVolumeKeyValues(USD_ScanSnapshot snapshot, VolumeProfile profile)
        {
            if (profile.TryGet(out ColorAdjustments ca) && ca.active)
            {
                snapshot.SetVolumeKey("CA.postExposure", ca.postExposure.value);
                snapshot.SetVolumeKey("CA.contrast", ca.contrast.value);
                snapshot.SetVolumeKey("CA.saturation", ca.saturation.value);
            }

            if (profile.TryGet(out WhiteBalance wb) && wb.active)
            {
                snapshot.SetVolumeKey("WB.temperature", wb.temperature.value);
                snapshot.SetVolumeKey("WB.tint", wb.tint.value);
            }

            if (profile.TryGet(out Vignette vig) && vig.active)
            {
                snapshot.SetVolumeKey("Vig.intensity", vig.intensity.value);
                snapshot.SetVolumeKey("Vig.smoothness", vig.smoothness.value);
            }

            if (profile.TryGet(out Bloom bloom) && bloom.active)
            {
                snapshot.SetVolumeKey("Bloom.intensity", bloom.intensity.value);
                snapshot.SetVolumeKey("Bloom.scatter", bloom.scatter.value);
            }

            if (profile.TryGet(out FilmGrain grain) && grain.active)
            {
                snapshot.SetVolumeKey("Grain.intensity", grain.intensity.value);
                snapshot.warnings.Add("FilmGrain.type=" + grain.type.value);
            }

            if (profile.TryGet(out ColorCurves curves) && curves.active)
            {
                var keyCount = curves.master.value != null && curves.master.value.keys != null ? curves.master.value.keys.Length : 0;
                snapshot.curvePreset = keyCount >= 4 ? "S_Curve_Strong" : "S_Curve_Soft";
            }

            if (profile.TryGet(out ShadowsMidtonesHighlights smh) && smh.active)
            {
                var s = smh.shadows.value;
                var h = smh.highlights.value;
                snapshot.smhShadowsBias = (s.x + s.y + s.z) / 3f;
                snapshot.smhHighlightsBias = (h.x + h.y + h.z) / 3f;
            }
        }

        private static void TryCaptureRendererData(USD_ScanSnapshot snapshot, UniversalRenderPipelineAsset urp)
        {
            try
            {
                var urpType = urp.GetType();
                var prop = urpType.GetProperty("scriptableRendererData");
                if (prop != null)
                {
                    var rdata = prop.GetValue(urp) as UnityEngine.Object;
                    if (rdata != null)
                    {
                        var rdPath = AssetDatabase.GetAssetPath(rdata);
                        snapshot.activeRendererDataGuid = AssetDatabase.AssetPathToGUID(rdPath);
                        snapshot.activeRendererDataName = rdata.name;
                        return;
                    }
                }

                var prop2 = urpType.GetProperty("rendererData");
                if (prop2 != null)
                {
                    var rdata = prop2.GetValue(urp) as UnityEngine.Object;
                    if (rdata != null)
                    {
                        var rdPath = AssetDatabase.GetAssetPath(rdata);
                        snapshot.activeRendererDataGuid = AssetDatabase.AssetPathToGUID(rdPath);
                        snapshot.activeRendererDataName = rdata.name;
                        return;
                    }
                }

                snapshot.warnings.Add("RendererData not accessible on this URP version (skipped).");
            }
            catch (Exception e)
            {
                snapshot.warnings.Add("RendererData reflection failed: " + e.Message);
            }
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
    }
}
