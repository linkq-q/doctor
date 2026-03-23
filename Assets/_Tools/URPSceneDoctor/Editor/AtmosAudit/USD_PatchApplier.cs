using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPSceneDoctor.Editor
{
    public static class USD_PatchApplier
    {
        public static List<string> ApplyPatch(string sceneName, string timestamp, USD_ApplyMode mode, USD_StyleProfileAsset styleProfile, bool assignProfileToExistingGlobalVolume)
        {
            var changes = new List<string>();
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            Volume global = null;
            var hadExistingGlobal = false;

            foreach (var volume in volumes)
            {
                if (volume != null && volume.isGlobal)
                {
                    global = volume;
                    hadExistingGlobal = true;
                    break;
                }
            }

            if (global == null)
            {
                var go = new GameObject("USD_GlobalVolume");
                USD_EditorUtil.RegisterCreatedObject(go, "Create USD_GlobalVolume");
                global = go.AddComponent<Volume>();
                global.isGlobal = true;
                changes.Add("Created new USD_GlobalVolume GameObject.");
            }

            var patchDir = USD_EditorUtil.EnsureSceneSubFolder("Patches", sceneName);
            var profilePath = USD_EditorUtil.EnsureUniqueAssetPath($"{patchDir}/{timestamp}_USD_Profile.asset");
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            USD_EditorUtil.RecordObject(profile, "Configure USD Volume Profile");

            ApplyNeutral(profile);
            changes.Add("ApplyMode=" + mode);
            changes.Add("StyleProfile=" + (styleProfile != null ? styleProfile.profileName : "Neutral Baseline"));

            if (mode == USD_ApplyMode.VisibleDemo)
            {
                ApplyStyleProfileToProfile(profile, styleProfile, changes);
            }

            if (!hadExistingGlobal || assignProfileToExistingGlobalVolume)
            {
                USD_EditorUtil.RecordObject(global, "Assign USD Volume Profile");
                global.sharedProfile = profile;
                changes.Add("Assigned new profile to global volume.");
            }
            else
            {
                changes.Add("BindPolicy=OFF (existing global volume kept unchanged)");
            }

            changes.Add("Created VolumeProfile: " + profilePath);
            AssetDatabase.SaveAssets();
            return changes;
        }

        private static void ApplyNeutral(VolumeProfile profile)
        {
            if (!profile.TryGet(out Tonemapping tonemapping)) tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.Neutral;

            if (!profile.TryGet(out ColorAdjustments colorAdj)) colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.overrideState = true;
            colorAdj.postExposure.value = 0f;
            colorAdj.contrast.overrideState = true;
            colorAdj.contrast.value = 0f;
            colorAdj.saturation.overrideState = true;
            colorAdj.saturation.value = 0f;

            if (!profile.TryGet(out WhiteBalance wb)) wb = profile.Add<WhiteBalance>(true);
            wb.temperature.overrideState = true;
            wb.temperature.value = 0f;
            wb.tint.overrideState = true;
            wb.tint.value = 0f;

            if (!profile.TryGet(out Vignette vignette)) vignette = profile.Add<Vignette>(true);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0f;

            if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
            bloom.active = false;
        }

        private static void ApplyStyleProfileToProfile(VolumeProfile profile, USD_StyleProfileAsset styleProfile, List<string> changes)
        {
            var style = styleProfile;
            if (style == null)
            {
                var builtIns = USD_StyleProfileUtil.GetOrCreateBuiltIns();
                style = builtIns.Length > 1 ? builtIns[1] : null;
            }

            if (style == null) return;
            var t = Mathf.Clamp01(style.visibleIntensity);

            if (!profile.TryGet(out Tonemapping tonemapping)) tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = style.tonemappingMode;

            if (!profile.TryGet(out ColorAdjustments colorAdj)) colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.overrideState = true;
            colorAdj.postExposure.value = Mathf.Lerp(style.postExposureMin, style.postExposureMax, t);
            colorAdj.contrast.overrideState = true;
            colorAdj.contrast.value = Mathf.Lerp(style.contrastMin, style.contrastMax, t);
            colorAdj.saturation.overrideState = true;
            colorAdj.saturation.value = Mathf.Lerp(style.saturationMin, style.saturationMax, t);

            if (!profile.TryGet(out WhiteBalance wb)) wb = profile.Add<WhiteBalance>(true);
            wb.temperature.overrideState = true;
            wb.temperature.value = Mathf.Lerp(style.temperatureMin, style.temperatureMax, t);
            wb.tint.overrideState = true;
            wb.tint.value = Mathf.Lerp(style.tintMin, style.tintMax, t);

            if (!profile.TryGet(out Vignette vignette)) vignette = profile.Add<Vignette>(true);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = Mathf.Lerp(style.vignetteMin, style.vignetteMax, t);

            if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
            bloom.active = style.bloomEnabled;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = style.bloomEnabled ? Mathf.Lerp(style.bloomIntensityMin, style.bloomIntensityMax, t) : 0f;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = Mathf.Lerp(style.bloomThresholdMin, style.bloomThresholdMax, t);

            changes.Add($"KeyVisualKnobs: WB.temp={wb.temperature.value:0.##}, Contrast={colorAdj.contrast.value:0.##}, Saturation={colorAdj.saturation.value:0.##}, PostExposure={colorAdj.postExposure.value:0.##}, Vignette={vignette.intensity.value:0.##}");
        }
    }
}
