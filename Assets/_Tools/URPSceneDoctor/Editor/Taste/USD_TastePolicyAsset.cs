using System;
using System.Collections.Generic;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    [Serializable]
    public sealed class USD_WeightedCategory
    {
        public string subCategory;
        public float weight = 1f;
    }

    [Serializable]
    public sealed class USD_PolicyHardRule
    {
        public string key;
        public float target;
        public float tolerance = 0.05f;
    }

    public sealed class USD_TastePolicyAsset : ScriptableObject
    {
        public string policyName = "YourTAStyle_v1";
        public string version = "1.0";

        public List<string> priorityOrder = new List<string>
        {
            "Luma structure",
            "Focus/Contrast",
            "Hue bias",
            "Accent contrast",
            "Vignette/Bloom/Grain",
            "Curves/SMH"
        };

        public List<string> forbiddenActions = new List<string>
        {
            "DoNotOverBloom",
            "AvoidRandomFilmGrainType",
            "AvoidPureContrastPush"
        };

        public float p0Weight = 1f;
        public float p1Weight = 0.7f;
        public float p2Weight = 0.4f;

        public List<USD_WeightedCategory> categoryWeights = new List<USD_WeightedCategory>
        {
            new USD_WeightedCategory { subCategory = "Readability", weight = 1f },
            new USD_WeightedCategory { subCategory = "Depth", weight = 0.9f },
            new USD_WeightedCategory { subCategory = "ColorMood", weight = 0.8f },
            new USD_WeightedCategory { subCategory = "Polish", weight = 0.6f },
            new USD_WeightedCategory { subCategory = "Fog", weight = 0.85f },
            new USD_WeightedCategory { subCategory = "Volumetric", weight = 0.85f }
        };

        public List<USD_PolicyHardRule> hardRules = new List<USD_PolicyHardRule>
        {
            new USD_PolicyHardRule { key = "Vig.intensity", target = 0.2f, tolerance = 0.02f },
            new USD_PolicyHardRule { key = "Vig.smoothness", target = 0.2f, tolerance = 0.02f },
            new USD_PolicyHardRule { key = "Grain.intensity", target = 0.6f, tolerance = 0.05f },
            new USD_PolicyHardRule { key = "Bloom.scatter", target = 0.5f, tolerance = 0.05f }
        };

        public float bloomHighTarget = 0.1f;
        public float bloomMidTarget = 1f;
        public float bloomLowTarget = 10f;
        public float bloomCeiling = 5f;

        public float warmTempMin = 5f;
        public float warmTempMax = 20f;
        public float coolTempMin = -20f;
        public float coolTempMax = -5f;
        public float tintMin = -5f;
        public float tintMax = 5f;

        public float lumaStdThreshold = 0.12f;
        public string platformMode = "Balanced";
    }
}
