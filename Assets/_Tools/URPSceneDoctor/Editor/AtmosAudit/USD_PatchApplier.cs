using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace URPSceneDoctor.Editor
{
    public static class USD_PatchApplier
    {
        public static List<string> ApplySafeAtmospherePatch(string sceneName, string timestamp, bool replaceExistingBinding)
        {
            var changes = new List<string>();
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            Volume global = null;
            foreach (var volume in volumes)
            {
                if (volume != null && volume.isGlobal)
                {
                    global = volume;
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
            var profilePath = $"{patchDir}/{timestamp}_USD_Profile.asset";
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            USD_EditorUtil.RecordObject(profile, "Configure USD Volume Profile");

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            var colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.contrast.Override(0f);
            colorAdj.saturation.Override(0f);
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(0f);
            wb.tint.Override(0f);

            profile.TryGet(out Bloom bloom);
            if (bloom != null) bloom.active = false;
            profile.TryGet(out Vignette vignette);
            if (vignette != null) vignette.active = false;

            if (global.sharedProfile == null || replaceExistingBinding)
            {
                USD_EditorUtil.RecordObject(global, "Assign USD Volume Profile");
                global.sharedProfile = profile;
                changes.Add("Assigned new profile to global volume.");
            }
            else
            {
                changes.Add("Existing global volume profile retained; new profile generated only.");
            }

            changes.Add("Created VolumeProfile: " + profilePath);
            AssetDatabase.SaveAssets();
            return changes;
        }
    }
}
