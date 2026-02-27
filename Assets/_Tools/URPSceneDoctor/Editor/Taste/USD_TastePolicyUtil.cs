using UnityEngine;
using UnityEditor;

namespace URPSceneDoctor.Editor
{
    public static class USD_TastePolicyUtil
    {
        public const string DefaultPolicyPath = "Assets/_Tools/URPSceneDoctor/Config/TastePolicies/DefaultTastePolicy.asset";

        public static USD_TastePolicyAsset GetOrCreateDefaultPolicy()
        {
            var policy = AssetDatabase.LoadAssetAtPath<USD_TastePolicyAsset>(DefaultPolicyPath);
            if (policy != null) return policy;

            USD_EditorUtil.EnsureFolder("Assets/_Tools/URPSceneDoctor/Config/TastePolicies");
            policy = ScriptableObject.CreateInstance<USD_TastePolicyAsset>();
            AssetDatabase.CreateAsset(policy, DefaultPolicyPath);
            AssetDatabase.SaveAssets();
            return policy;
        }

        public static float SeverityWeight(USD_TastePolicyAsset policy, string severity)
        {
            if (policy == null) return 1f;
            switch (severity)
            {
                case "P0": return policy.p0Weight;
                case "P1": return policy.p1Weight;
                case "P2": return policy.p2Weight;
                default: return 0.5f;
            }
        }

        public static float CategoryWeight(USD_TastePolicyAsset policy, string subCategory)
        {
            if (policy == null || policy.categoryWeights == null) return 1f;
            foreach (var item in policy.categoryWeights)
            {
                if (item.subCategory == subCategory) return item.weight;
            }

            return 0.8f;
        }

        public static int PriorityOrderIndex(USD_TastePolicyAsset policy, string subCategory)
        {
            if (policy == null || policy.priorityOrder == null) return 99;
            var index = policy.priorityOrder.IndexOf(subCategory);
            return index < 0 ? 99 : index;
        }
    }
}
