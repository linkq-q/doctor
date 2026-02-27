using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPSceneDoctor.Editor
{
    public static class USD_PatchApplier
    {
        public static List<string> ApplySafeAtmospherePatch(string sceneName, string timestamp, bool assignProfileToExistingGlobalVolume)
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
            var profilePath = $"{patchDir}/{timestamp}_USD_Profile.asset";
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            USD_EditorUtil.RecordObject(profile, "Configure USD Volume Profile");

            // 获取或创建 Tonemapping
            if (!profile.TryGet(out Tonemapping tonemapping))
                tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.Neutral;

            // 获取或创建 ColorAdjustments
            if (!profile.TryGet(out ColorAdjustments colorAdj))
                colorAdj = profile.Add<ColorAdjustments>(true);
            // 这里保持“中性”，不要强行风格化
            colorAdj.contrast.overrideState = true;
            colorAdj.contrast.value = 0f;
            colorAdj.saturation.overrideState = true;
            colorAdj.saturation.value = 0f;

            // WhiteBalance（如果你有用）
            if (!profile.TryGet(out WhiteBalance wb))
                wb = profile.Add<WhiteBalance>(true);
            wb.temperature.overrideState = true;
            wb.temperature.value = 0f;

            if (!hadExistingGlobal || assignProfileToExistingGlobalVolume)
            {
                USD_EditorUtil.RecordObject(global, "Assign USD Volume Profile");
                global.sharedProfile = profile;
                changes.Add("Assigned new profile to global volume.");
            }
            else
            {
                changes.Add("Existing global volume kept unchanged (profile generated only, no rebind).");
            }

            changes.Add("Created VolumeProfile: " + profilePath);
            AssetDatabase.SaveAssets();
            return changes;
        }
    }
}
