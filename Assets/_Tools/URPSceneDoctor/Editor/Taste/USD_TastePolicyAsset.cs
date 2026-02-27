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

    public sealed class USD_TastePolicyAsset : ScriptableObject
    {
        public string policyName = "DefaultTastePolicy";
        public string version = "0.3";
        public List<string> priorityOrder = new List<string> { "Readability", "Depth", "ColorMood", "Polish" };
        public List<string> forbiddenActions = new List<string> { "DoNotOverBloom", "DoNotCrushBlacks", "AvoidOverExposure" };
        public float p0Weight = 1f;
        public float p1Weight = 0.7f;
        public float p2Weight = 0.4f;
        public List<USD_WeightedCategory> categoryWeights = new List<USD_WeightedCategory>
        {
            new USD_WeightedCategory { subCategory = "Readability", weight = 1f },
            new USD_WeightedCategory { subCategory = "Depth", weight = 0.9f },
            new USD_WeightedCategory { subCategory = "ColorMood", weight = 0.8f },
            new USD_WeightedCategory { subCategory = "Polish", weight = 0.6f }
        };
        public string platformMode = "Balanced";
    }
}
