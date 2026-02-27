using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public static class USD_StyleProfileUtil
    {
        public const string Root = "Assets/_Tools/URPSceneDoctor/Config/StyleProfiles";
        public const string NeutralPath = Root + "/Neutral_Baseline.asset";
        public const string CleanPath = Root + "/Clean_Stylized.asset";
        public const string WarmPath = Root + "/Warm_Dusk.asset";
        public const string MoodyPath = Root + "/Moody_Cool.asset";

        public static USD_StyleProfileAsset[] GetOrCreateBuiltIns()
        {
            USD_EditorUtil.EnsureFolder(Root);
            return new[]
            {
                GetOrCreate(NeutralPath, "Neutral Baseline", "Near-neutral baseline profile.", 0.15f, 0, 0, 0, 0, 0, 0, 0, 0, false),
                GetOrCreate(CleanPath, "Clean Stylized", "Clear, clean stylized look.", 0.75f, 0.05f, 0.15f, 10, 18, 5, 12, 2, 6, 0.05f, 0.12f, false),
                GetOrCreate(WarmPath, "Warm Dusk", "Warm dusk mood.", 0.8f, 0.05f, 0.15f, 8, 16, 6, 14, 10, 18, 0.05f, 0.1f, false),
                GetOrCreate(MoodyPath, "Moody Cool", "Cool moody atmosphere.", 0.8f, 0.0f, 0.08f, 12, 22, -2, 6, -15, -8, 0.08f, 0.18f, false)
            };
        }

        private static USD_StyleProfileAsset GetOrCreate(string path, string name, string desc, float intensity,
            float peMin, float peMax, float cMin, float cMax, float sMin, float sMax, float tMin, float tMax,
            float vMin, float vMax, bool bloom)
        {
            var asset = AssetDatabase.LoadAssetAtPath<USD_StyleProfileAsset>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<USD_StyleProfileAsset>();
            asset.profileName = name;
            asset.description = desc;
            asset.visibleIntensity = intensity;
            asset.postExposureMin = peMin;
            asset.postExposureMax = peMax;
            asset.contrastMin = cMin;
            asset.contrastMax = cMax;
            asset.saturationMin = sMin;
            asset.saturationMax = sMax;
            asset.temperatureMin = tMin;
            asset.temperatureMax = tMax;
            asset.vignetteMin = vMin;
            asset.vignetteMax = vMax;
            asset.bloomEnabled = bloom;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
