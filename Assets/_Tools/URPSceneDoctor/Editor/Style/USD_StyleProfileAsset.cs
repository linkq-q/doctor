using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace URPSceneDoctor.Editor
{
    [CreateAssetMenu(menuName = "URP Scene Doctor/Style Profile", fileName = "USD_StyleProfile")]
    public sealed class USD_StyleProfileAsset : ScriptableObject
    {
        public string profileName = "Neutral Baseline";
        [TextArea] public string description;
        [Range(0f, 1f)] public float visibleIntensity = 0.75f;

        public TonemappingMode tonemappingMode = TonemappingMode.Neutral;

        public float postExposureMin;
        public float postExposureMax;
        public float contrastMin;
        public float contrastMax;
        public float saturationMin;
        public float saturationMax;

        public float temperatureMin;
        public float temperatureMax;
        public float tintMin;
        public float tintMax;

        public float vignetteMin;
        public float vignetteMax;

        public bool bloomEnabled;
        public float bloomIntensityMin;
        public float bloomIntensityMax;
        public float bloomThresholdMin = 1f;
        public float bloomThresholdMax = 1f;
    }
}
