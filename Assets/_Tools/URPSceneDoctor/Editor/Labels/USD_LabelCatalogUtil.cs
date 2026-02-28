using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public static class USD_LabelCatalogUtil
    {
        public const string DefaultPath = "Assets/_Tools/URPSceneDoctor/Config/LabelCatalogs/DefaultLabelCatalog.asset";

        public static USD_LabelCatalogAsset GetOrCreateDefault()
        {
            var c = AssetDatabase.LoadAssetAtPath<USD_LabelCatalogAsset>(DefaultPath);
            if (c != null) return c;

            USD_EditorUtil.EnsureFolder("Assets/_Tools/URPSceneDoctor/Config/LabelCatalogs");
            c = ScriptableObject.CreateInstance<USD_LabelCatalogAsset>();
            c.languageDefault = "zh";
            c.styles = new List<USD_StyleGoal>
            {
                new USD_StyleGoal { id = "clean", name_zh = "清透风格", name_en = "Clean Stylized", defaultStyleProfileId = "Clean Stylized" },
                new USD_StyleGoal { id = "warm_dusk", name_zh = "暖黄昏", name_en = "Warm Dusk", defaultStyleProfileId = "Warm Dusk" },
                new USD_StyleGoal { id = "moody_cool", name_zh = "冷氛围", name_en = "Moody Cool", defaultStyleProfileId = "Moody Cool" }
            };
            c.issues = new List<USD_IssueTag>
            {
                new USD_IssueTag { id = "contrast_flat", name_zh = "对比不足", name_en = "Low contrast" },
                new USD_IssueTag { id = "highlight_burn", name_zh = "高光过曝", name_en = "Highlight clipping" },
                new USD_IssueTag { id = "shadow_dirty", name_zh = "暗部脏", name_en = "Dirty shadows" }
            };
            AssetDatabase.CreateAsset(c, DefaultPath);
            AssetDatabase.SaveAssets();
            return c;
        }

        public static bool HasDuplicateIds(USD_LabelCatalogAsset c, out string dup)
        {
            dup = string.Empty;
            if (c == null) return false;
            var set = new HashSet<string>();
            foreach (var s in c.styles)
            {
                if (s == null || string.IsNullOrEmpty(s.id)) continue;
                if (!set.Add("S:" + s.id)) { dup = s.id; return true; }
            }

            foreach (var i in c.issues)
            {
                if (i == null || string.IsNullOrEmpty(i.id)) continue;
                if (!set.Add("I:" + i.id)) { dup = i.id; return true; }
            }
            return false;
        }

        public static string DisplayStyle(USD_StyleGoal s)
        {
            if (s == null) return "(null)";
            return USD_Localization.IsZh() ? s.name_zh : s.name_en;
        }

        public static string DisplayIssue(USD_IssueTag i)
        {
            if (i == null) return "(null)";
            return USD_Localization.IsZh() ? i.name_zh : i.name_en;
        }
    }
}
